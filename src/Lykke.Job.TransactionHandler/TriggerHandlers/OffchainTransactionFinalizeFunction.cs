using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.MarginTrading;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services.AppNotifications;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.MarginTrading;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Resources;
using Lykke.Job.TransactionHandler.Services.Notifications;
using Lykke.JobTriggers.Triggers.Attributes;
using Lykke.Service.Assets.Client;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.ExchangeOperations.Client;
using Lykke.Service.Operations.Client;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Lykke.Service.PersonalData.Contract;

namespace Lykke.Job.TransactionHandler.TriggerHandlers
{
    public class OffchainTransactionFinalizeFunction
    {
        private readonly ITransactionsRepository _transactionsRepository;
        private readonly ITransactionService _transactionService;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly ITradeOperationsRepositoryClient _clientTradesRepositoryClient;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IPersonalDataService _personalDataService;
        private readonly IOffchainTransferRepository _offchainTransferRepository;
        private readonly ITransferOperationsRepositoryClient _transferEventsRepositoryClient;
        private readonly IPaymentTransactionsRepository _paymentTransactionsRepository;
        private readonly IAppNotifications _appNotifications;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;

        private readonly IMarginDataServiceResolver _marginDataServiceResolver;
        private readonly IMarginTradingPaymentLogRepository _marginTradingPaymentLog;


        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly IExchangeOperationsServiceClient _exchangeOperationsService;
        private readonly SrvSlackNotifications _srvSlackNotifications;
        private readonly ILog _log;
        
        private readonly IOperationsClient _operationsClient;

        public OffchainTransactionFinalizeFunction(
            ITransactionsRepository transactionsRepository,
            ILog log,
            ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IExchangeOperationsServiceClient exchangeOperationsService,
            SrvSlackNotifications srvSlackNotifications,
            ISrvEmailsFacade srvEmailsFacade,
            ITradeOperationsRepositoryClient clientTradesRepositoryClient,
            IClientAccountClient clientAccountClient,
            IPersonalDataService personalDataService,
            IOffchainTransferRepository offchainTransferRepository,
            ITransferOperationsRepositoryClient transferEventsRepositoryClient,
            IMarginDataServiceResolver marginDataServiceResolver,
            IMarginTradingPaymentLogRepository marginTradingPaymentLog,
            IPaymentTransactionsRepository paymentTransactionsRepository,
            IAppNotifications appNotifications,
            ITransactionService transactionService,
            IOperationsClient operationsClient, 
            IAssetsServiceWithCache assetsServiceWithCache)
        {
            _transactionsRepository = transactionsRepository;
            _log = log;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _exchangeOperationsService = exchangeOperationsService;
            _srvSlackNotifications = srvSlackNotifications;
            _srvEmailsFacade = srvEmailsFacade;
            _clientTradesRepositoryClient = clientTradesRepositoryClient;
            _clientAccountClient = clientAccountClient;
            _personalDataService = personalDataService;
            _offchainTransferRepository = offchainTransferRepository;
            _transferEventsRepositoryClient = transferEventsRepositoryClient;

            _marginDataServiceResolver = marginDataServiceResolver;
            _marginTradingPaymentLog = marginTradingPaymentLog;
            _paymentTransactionsRepository = paymentTransactionsRepository;
            _appNotifications = appNotifications;
            _transactionService = transactionService;
            _operationsClient = operationsClient;
            _assetsServiceWithCache = assetsServiceWithCache;
        }

        [QueueTrigger("offchain-finalization", notify: true, maxDequeueCount: 1, maxPollingIntervalMs: 100)]
        public async Task Process(OffchainFinalizetionMessage message)
        {
            var transfer = await _offchainTransferRepository.GetTransfer(message.TransferId);

            if (transfer.Type == OffchainTransferType.HubCashout || transfer.Type == OffchainTransferType.CashinFromClient || transfer.Type == OffchainTransferType.TrustedCashout)
                return;

            var transactionId =
                transfer.Type == OffchainTransferType.FromClient || transfer.Type == OffchainTransferType.FromHub
                    ? transfer.OrderId
                    : transfer.Id;

            var transaction = await _transactionsRepository.SaveResponseAndHashAsync(transactionId, null, message.TransactionHash);

            if (transaction == null)
            {
                await _log.WriteWarningAsync(nameof(OffchainTransactionFinalizeFunction), nameof(Process), $"Transaction: {transactionId}, client: {message.ClientId}, hash: {message.TransactionHash}, transfer: {message.TransferId}", "unkown transaction");
                return;
            }

            switch (transaction.CommandType)
            {
                case BitCoinCommands.Issue:
                    await FinalizeIssue(transaction);
                    break;
                case BitCoinCommands.CashOut:
                    await FinalizeCashOut(transaction, transfer);
                    break;
                case BitCoinCommands.Transfer:
                    await FinalizeTransfer(transaction, transfer);
                    break;
                case BitCoinCommands.SwapOffchain:
                    await FinalizeSwap(transaction, transfer);
                    break;
                default:
                    await _log.WriteWarningAsync(nameof(OffchainTransactionFinalizeFunction), nameof(Process), $"Transaction: {transactionId}, client: {message.ClientId}, hash: {message.TransactionHash}, transfer: {message.TransferId}", "unkown command type");
                    break;
            }
        }

        private async Task FinalizeIssue(IBitcoinTransaction transaction)
        {
            var contextData = await _transactionService.GetTransactionContext<IssueContextData>(transaction.TransactionId);

            await _cashOperationsRepositoryClient.SetIsSettledAsync(contextData.ClientId, contextData.CashOperationId, true);
        }

        private async Task FinalizeTransfer(IBitcoinTransaction transaction, IOffchainTransfer transfer)
        {
            var contextData = await _transactionService.GetTransactionContext<TransferContextData>(transaction.TransactionId);

            switch (contextData.TransferType)
            {
                case TransferType.ToMarginAccount:
                    await FinalizeTransferToMargin(contextData, transfer);
                    return;
                case TransferType.ToTrustedWallet:
                    await FinalizeTransferToTrustedWallet(transaction, contextData, transfer);
                    return;
                case TransferType.Common:
                    await FinalizeCommonTransfer(transaction, contextData);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        "contextData.TransferType", 
                        contextData.TransferType.ToString(),
                        $"TransactionId: {transaction.TransactionId}. Expected values are: ToMarginAccount, ToTrustedWallet, Common");
            }
        }

        private async Task FinalizeTransferToMargin(TransferContextData context, IOffchainTransfer transfer)
        {
            var sourceTransferContext = context.Transfers.FirstOrDefault(x => x.ClientId == transfer.ClientId);
            var destTransferContext = context.Transfers.FirstOrDefault(x => x.ClientId != transfer.ClientId);

            if (sourceTransferContext?.Actions?.UpdateMarginBalance == null)
                throw new Exception();

            var exchangeOperationResult = await _exchangeOperationsService.FinishTransferAsync(
                transfer.Id,
                sourceTransferContext.ClientId,
                destTransferContext.ClientId,
                (double)transfer.Amount,
                transfer.AssetId);
            
            if (!exchangeOperationResult.IsOk())
            {
                var errorLog = MarginTradingPaymentLog.CreateError(transfer.ClientId,
                    sourceTransferContext.Actions.UpdateMarginBalance.AccountId, DateTime.UtcNow,
                    (double) sourceTransferContext.Actions.UpdateMarginBalance.Amount,
                    $"Transfer to margin wallet failed; transfer: {transfer.Id}, ME result: {exchangeOperationResult.ToJson()}");

                await _marginTradingPaymentLog.CreateAsync(errorLog);
                await _srvSlackNotifications.SendNotification(ChannelTypes.MarginTrading, errorLog.ToJson(), "Transaction handler");

                return;
            }

            var marginDataService = _marginDataServiceResolver.Resolve(false);

            var depositToMarginResult = await marginDataService.DepositToAccount(transfer.ClientId,
                sourceTransferContext.Actions.UpdateMarginBalance.AccountId,
                (double) sourceTransferContext.Actions.UpdateMarginBalance.Amount,
                MarginPaymentType.Transfer);

            if (depositToMarginResult.IsOk)
                await _marginTradingPaymentLog.CreateAsync(MarginTradingPaymentLog.CreateOk(transfer.ClientId,
                    sourceTransferContext.Actions.UpdateMarginBalance.AccountId, DateTime.UtcNow,
                    (double) sourceTransferContext.Actions.UpdateMarginBalance.Amount, transfer.ExternalTransferId));
            else
            {
                var errorLog = MarginTradingPaymentLog.CreateError(transfer.ClientId,
                    sourceTransferContext.Actions.UpdateMarginBalance.AccountId, DateTime.UtcNow,
                    (double) sourceTransferContext.Actions.UpdateMarginBalance.Amount,
                    $"Error deposit to margin account: {depositToMarginResult.ErrorMessage}");

                await _marginTradingPaymentLog.CreateAsync(errorLog);
                await _srvSlackNotifications.SendNotification(ChannelTypes.MarginTrading, errorLog.ToJson(), "Transaction handler");
            }

        }

        private async Task FinalizeTransferToTrustedWallet(IBitcoinTransaction transaction, TransferContextData context, IOffchainTransfer transfer)
        {
            var clientId = context.Transfers.First(x => x.ClientId == transfer.ClientId).ClientId;
            var walletId = context.Transfers.First(x => x.ClientId != transfer.ClientId).ClientId;

            var exchangeOperationResult = await _exchangeOperationsService.FinishTransferAsync(
                transfer.Id,
                clientId,
                walletId,
                (double)transfer.Amount,
                transfer.AssetId);

            if (!exchangeOperationResult.IsOk())
            {
                await _log.WriteWarningAsync(nameof(OffchainTransactionFinalizeFunction),
                    nameof(FinalizeTransferToTrustedWallet), exchangeOperationResult.ToJson(), "ME operation failed");
                await _srvSlackNotifications.SendNotification(ChannelTypes.Errors,
                    $"Transfer to trusted wallet failed; client: {transfer.ClientId}, transfer: {transfer.Id}, ME code result: {exchangeOperationResult.Code}");

                await _paymentTransactionsRepository.SetStatus(transaction.TransactionId, PaymentStatus.NotifyDeclined);
            }
            else
            {
                await _paymentTransactionsRepository.SetStatus(transaction.TransactionId, PaymentStatus.NotifyProcessed);
            }

            await _operationsClient.Complete(new Guid(transaction.TransactionId));
        }

        private async Task FinalizeCommonTransfer(IBitcoinTransaction transaction, TransferContextData contextData)
        {
            foreach (var transfer in contextData.Transfers)
            {
                await _transferEventsRepositoryClient.SetIsSettledIfExistsAsync(transfer.ClientId, transfer.OperationId, true);

                var clientData = await _personalDataService.GetAsync(transfer.ClientId);
                var clientAcc = await _clientAccountClient.GetByIdAsync(transfer.ClientId);

                if (transfer.Actions?.CashInConvertedOkEmail != null)
                {
                    await
                        _srvEmailsFacade.SendTransferCompletedEmail(clientAcc.PartnerId, clientData.Email, clientData.FullName,
                            transfer.Actions.CashInConvertedOkEmail.AssetFromId, transfer.Actions.CashInConvertedOkEmail.AmountFrom,
                            transfer.Actions.CashInConvertedOkEmail.AmountLkk, transfer.Actions.CashInConvertedOkEmail.Price, transaction.BlockchainHash);
                }

                if (transfer.Actions?.SendTransferEmail != null)
                {
                    await
                        _srvEmailsFacade.SendDirectTransferCompletedEmail(clientAcc.PartnerId, clientData.Email, clientData.FullName,
                            transfer.Actions.SendTransferEmail.AssetId, transfer.Actions.SendTransferEmail.Amount,
                            transaction.BlockchainHash);
                }

                if (transfer.Actions?.PushNotification != null)
                {
                    var asset = await _assetsServiceWithCache.TryGetAssetAsync(transfer.Actions.PushNotification.AssetId);

                    await _appNotifications.SendAssetsCreditedNotification(new[] { clientAcc.NotificationsId },
                            (double) transfer.Actions.PushNotification.Amount, transfer.Actions.PushNotification.AssetId,
                            string.Format(TextResources.CreditedPushText, transfer.Actions.PushNotification.Amount.GetFixedAsString(asset.Accuracy),
                                transfer.Actions.PushNotification.AssetId));
                }
            }

            await _paymentTransactionsRepository.SetStatus(transaction.TransactionId, PaymentStatus.NotifyProcessed);
        }

        private async Task FinalizeCashOut(IBitcoinTransaction transaction, IOffchainTransfer offchainTransfer)
        {
            var data = await _exchangeOperationsService.FinishCashOutAsync(transaction.TransactionId, offchainTransfer.ClientId, (double)offchainTransfer.Amount, offchainTransfer.AssetId);

            if (!data.IsOk())
            {
                await _log.WriteWarningAsync("CashOutController", "CashOut", data.ToJson(), "ME operation failed");
                await _srvSlackNotifications.SendNotification(ChannelTypes.Errors, $"Cashout failed in ME, client: {offchainTransfer.ClientId}, transfer: {transaction.TransactionId}, ME code result: {data.Code}");
            }
        }

        private async Task FinalizeSwap(IBitcoinTransaction transaction, IOffchainTransfer offchainTransfer)
        {
            var transactionsContextData = new Dictionary<string, SwapOffchainContextData>();

            var allTransfers = new HashSet<string>(offchainTransfer.GetAdditionalData().ChildTransfers) { offchainTransfer.Id };

            foreach (var transferId in allTransfers)
            {
                try
                {
                    var transfer = await _offchainTransferRepository.GetTransfer(transferId);

                    if (!transactionsContextData.ContainsKey(transfer.OrderId))
                    {
                        var ctx = await _transactionService.GetTransactionContext<SwapOffchainContextData>(transaction.TransactionId);
                        if (ctx == null)
                            continue;

                        transactionsContextData.Add(transfer.OrderId, ctx);
                    }

                    var contextData = transactionsContextData[transfer.OrderId];

                    var operation = contextData.Operations.FirstOrDefault(x => x.TransactionId == transferId);

                    if (operation == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(operation?.ClientTradeId) || string.IsNullOrWhiteSpace(operation?.ClientId))
                    {
                        await _log.WriteWarningAsync(nameof(OffchainTransactionFinalizeFunction),
                            nameof(FinalizeSwap), operation?.ToJson(),
                            $"Missing fields. Client trade id {operation?.ClientTradeId}, client {operation?.ClientId}, transfer: {transferId}");
                        continue;
                    }

                    await Task.WhenAll(
                        _offchainTransferRepository.CompleteTransfer(transferId),
                        _clientTradesRepositoryClient.SetIsSettledAsync(operation.ClientId, operation.ClientTradeId, true)
                    );
                }
                catch (Exception e)
                {
                    await _log.WriteErrorAsync(nameof(OffchainTransactionFinalizeFunction), nameof(FinalizeSwap), $"Transfer: {transferId}", e);
                }
            }
        }
    }

    public class OffchainFinalizetionMessage
    {
        public string ClientId { get; set; }
        public string TransferId { get; set; }
        public string TransactionHash { get; set; }
    }
}
