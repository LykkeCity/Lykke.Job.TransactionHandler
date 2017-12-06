using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Domain.Assets;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.MarginTrading;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services.AppNotifications;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin.BitCoinApi;
using Lykke.Job.TransactionHandler.Core.Services.ChronoBank;
using Lykke.Job.TransactionHandler.Core.Services.MarginTrading;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Quanta;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
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
        private readonly IBitCoinTransactionsRepository _bitCoinTransactionsRepository;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly ICashOutAttemptRepository _cashOutAttemptRepository;
        private readonly IClientTradesRepository _clientTradesRepository;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IPersonalDataService _personalDataService;
        private readonly IOffchainTransferRepository _offchainTransferRepository;
        private readonly ITransferEventsRepository _transferEventsRepository;
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IBitcoinApiClient _bitcoinApiClient;
        private readonly IOffchainRequestRepository _offchainRequestRepository;
        private readonly CachedDataDictionary<string, IAssetSetting> _assetSettings;
        private readonly IPaymentTransactionsRepository _paymentTransactionsRepository;
        private readonly IAppNotifications _appNotifications;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;

        private readonly IMarginDataServiceResolver _marginDataServiceResolver;
        private readonly IMarginTradingPaymentLogRepository _marginTradingPaymentLog;


        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly IExchangeOperationsServiceClient _exchangeOperationsService;
        private readonly SrvSlackNotifications _srvSlackNotifications;
        private readonly ILog _log;

        private readonly IChronoBankService _chronoBankService;
        private readonly ISrvSolarCoinHelper _srvSolarCoinHelper;
        private readonly IQuantaService _quantaService;
        private readonly IOperationsClient _operationsClient;

        public OffchainTransactionFinalizeFunction(
            IBitCoinTransactionsRepository bitCoinTransactionsRepository,
            ILog log,
            ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IExchangeOperationsServiceClient exchangeOperationsService,
            SrvSlackNotifications srvSlackNotifications,
            ICashOutAttemptRepository cashOutAttemptRepository,
            ISrvEmailsFacade srvEmailsFacade,
            IClientTradesRepository clientTradesRepository,
            IClientAccountClient clientAccountClient,
            IPersonalDataService personalDataService,
            IOffchainTransferRepository offchainTransferRepository,
            IChronoBankService chronoBankService,
            ISrvSolarCoinHelper srvSolarCoinHelper,
            ITransferEventsRepository transferEventsRepository,
            IQuantaService quantaService,
            IOffchainRequestService offchainRequestService,
            IWalletCredentialsRepository walletCredentialsRepository,
            IBitcoinApiClient bitcoinApiClient,
            IOffchainRequestRepository offchainRequestRepository,
            CachedDataDictionary<string, IAssetSetting> assetSettings,
            IMarginDataServiceResolver marginDataServiceResolver,
            IMarginTradingPaymentLogRepository marginTradingPaymentLog,
            IPaymentTransactionsRepository paymentTransactionsRepository,
            IAppNotifications appNotifications,
            IBitcoinTransactionService bitcoinTransactionService,
            IOperationsClient operationsClient, 
            IAssetsServiceWithCache assetsServiceWithCache)
        {
            _bitCoinTransactionsRepository = bitCoinTransactionsRepository;
            _log = log;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _exchangeOperationsService = exchangeOperationsService;
            _srvSlackNotifications = srvSlackNotifications;
            _cashOutAttemptRepository = cashOutAttemptRepository;
            _srvEmailsFacade = srvEmailsFacade;
            _clientTradesRepository = clientTradesRepository;
            _clientAccountClient = clientAccountClient;
            _personalDataService = personalDataService;
            _offchainTransferRepository = offchainTransferRepository;
            _chronoBankService = chronoBankService;
            _srvSolarCoinHelper = srvSolarCoinHelper;
            _transferEventsRepository = transferEventsRepository;
            _quantaService = quantaService;
            _offchainRequestService = offchainRequestService;
            _walletCredentialsRepository = walletCredentialsRepository;
            _bitcoinApiClient = bitcoinApiClient;
            _offchainRequestRepository = offchainRequestRepository;
            _assetSettings = assetSettings;

            _marginDataServiceResolver = marginDataServiceResolver;
            _marginTradingPaymentLog = marginTradingPaymentLog;
            _paymentTransactionsRepository = paymentTransactionsRepository;
            _appNotifications = appNotifications;
            _bitcoinTransactionService = bitcoinTransactionService;
            _operationsClient = operationsClient;
            _assetsServiceWithCache = assetsServiceWithCache;
        }

        [QueueTrigger("offchain-finalization", notify: true, maxDequeueCount: 1, maxPollingIntervalMs: 100)]
        public async Task Process(OffchainFinalizetionMessage message)
        {
            var transfer = await _offchainTransferRepository.GetTransfer(message.TransferId);

            if (transfer.Type == OffchainTransferType.HubCashout || transfer.Type == OffchainTransferType.CashinFromClient)
                return;

            var transactionId =
                transfer.Type == OffchainTransferType.FromClient || transfer.Type == OffchainTransferType.FromHub
                    ? transfer.OrderId
                    : transfer.Id;

            var transaction = await _bitCoinTransactionsRepository.SaveResponseAndHashAsync(transactionId, null, message.TransactionHash);

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
            var contextData = await _bitcoinTransactionService.GetTransactionContext<IssueContextData>(transaction.TransactionId);

            await _cashOperationsRepositoryClient.SetIsSettledAsync(contextData.ClientId, contextData.CashOperationId, true);
        }

        private async Task FinalizeTransfer(IBitcoinTransaction transaction, IOffchainTransfer transfer)
        {
            var contextData = await _bitcoinTransactionService.GetTransactionContext<TransferContextData>(transaction.TransactionId);

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
                    sourceTransferContext.Actions.UpdateMarginBalance.Amount,
                    $"Transfer to margin wallet failed; transfer: {transfer.Id}, ME result: {exchangeOperationResult.ToJson()}");

                await _marginTradingPaymentLog.CreateAsync(errorLog);
                await _srvSlackNotifications.SendNotification(ChannelTypes.MarginTrading, errorLog.ToJson(), "Transaction handler");

                return;
            }

            var marginDataService = _marginDataServiceResolver.Resolve(false);

            var depositToMarginResult = await marginDataService.DepositToAccount(transfer.ClientId,
                sourceTransferContext.Actions.UpdateMarginBalance.AccountId,
                sourceTransferContext.Actions.UpdateMarginBalance.Amount,
                MarginPaymentType.Transfer);

            if (depositToMarginResult.IsOk)
                await _marginTradingPaymentLog.CreateAsync(MarginTradingPaymentLog.CreateOk(transfer.ClientId,
                    sourceTransferContext.Actions.UpdateMarginBalance.AccountId, DateTime.UtcNow,
                    sourceTransferContext.Actions.UpdateMarginBalance.Amount, transfer.ExternalTransferId));
            else
            {
                var errorLog = MarginTradingPaymentLog.CreateError(transfer.ClientId,
                    sourceTransferContext.Actions.UpdateMarginBalance.AccountId, DateTime.UtcNow,
                    sourceTransferContext.Actions.UpdateMarginBalance.Amount,
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
                await _transferEventsRepository.SetIsSettledIfExistsAsync(transfer.ClientId, transfer.OperationId, true);

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
                            transfer.Actions.PushNotification.Amount, transfer.Actions.PushNotification.AssetId,
                            string.Format(TextResources.CreditedPushText, transfer.Actions.PushNotification.Amount.GetFixedAsString(asset.Accuracy),
                                transfer.Actions.PushNotification.AssetId));
                }
            }

            await _paymentTransactionsRepository.SetStatus(transaction.TransactionId, PaymentStatus.NotifyProcessed);
        }

        private async Task FinalizeCashOut(IBitcoinTransaction transaction, IOffchainTransfer offchainTransfer)
        {
            var amount = Math.Abs((double)offchainTransfer.Amount);

            var data = await _exchangeOperationsService.FinishCashOutAsync(transaction.TransactionId, offchainTransfer.ClientId, (double)offchainTransfer.Amount, offchainTransfer.AssetId);

            await CreateHubCashoutIfNeed(offchainTransfer);

            if (!data.IsOk())
            {
                await _log.WriteWarningAsync("CashOutController", "CashOut", data.ToJson(), "ME operation failed");
                await _srvSlackNotifications.SendNotification(ChannelTypes.Errors, $"Cashout failed in ME, client: {offchainTransfer.ClientId}, transfer: {transaction.TransactionId}, ME code result: {data.Code}");
            }

            var contextData = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transaction.TransactionId);

            var swiftData = contextData.AddData?.SwiftData;
            if (swiftData != null)
            {
                await _cashOutAttemptRepository.SetIsSettledOffchain(contextData.ClientId, swiftData.CashOutRequestId);
            }
            else
            {
                if (offchainTransfer.AssetId == LykkeConstants.SolarAssetId)
                {
                    await PostSolarCashOut(offchainTransfer.ClientId, contextData.Address, amount, transaction.TransactionId);
                }
                else if (offchainTransfer.AssetId == LykkeConstants.ChronoBankAssetId)
                {
                    await PostChronoBankCashOut(contextData.Address, amount, transaction.TransactionId);
                }
                else if (offchainTransfer.AssetId == LykkeConstants.QuantaAssetId)
                {
                    await PostQuantaCashOut(contextData.Address, amount, transaction.TransactionId);
                }
                else
                {
                    var clientData = await _personalDataService.GetAsync(contextData.ClientId);
                    var clientAcc = await _clientAccountClient.GetByIdAsync(contextData.ClientId);
                    await _srvEmailsFacade.SendNoRefundOCashOutMail(clientAcc.PartnerId, clientData.Email, contextData.Amount, contextData.AssetId, transaction.BlockchainHash);
                }
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
                        var ctx = await _bitcoinTransactionService.GetTransactionContext<SwapOffchainContextData>(transaction.TransactionId);
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
                        _clientTradesRepository.SetIsSettledAsync(operation.ClientId, operation.ClientTradeId, true)
                    );
                }
                catch (Exception e)
                {
                    await _log.WriteErrorAsync(nameof(OffchainTransactionFinalizeFunction), nameof(FinalizeSwap), $"Transfer: {transferId}", e);
                }
            }
        }

        private Task PostChronoBankCashOut(string address, double amount, string txId)
        {
            return _chronoBankService.SendCashOutRequest(txId, address, amount);
        }

        private Task PostQuantaCashOut(string address, double amount, string txId)
        {
            return _quantaService.SendCashOutRequest(txId, address, amount);
        }

        private async Task PostSolarCashOut(string clientId, string address, double amount, string txId)
        {
            var slrAddress = new SolarCoinAddress(address);
            var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);

            var sendEmailTask = _srvEmailsFacade.SendSolarCashOutCompletedEmail(clientAcc.PartnerId, clientAcc.Email, slrAddress.Value, amount);
            var solarRequestTask = _srvSolarCoinHelper.SendCashOutRequest(txId, slrAddress, amount);

            await Task.WhenAll(sendEmailTask, solarRequestTask);
        }

        private async Task CreateHubCashoutIfNeed(IOffchainTransfer offchainTransfer)
        {
            try
            {
                var client = await _walletCredentialsRepository.GetAsync(offchainTransfer.ClientId);

                var currentRequests = (await _offchainRequestRepository.GetRequestsForClient(offchainTransfer.ClientId)).ToList();
                var currentChannels = await _bitcoinApiClient.Balances(client.MultiSig);

                var hasBtcRequest =
                    currentRequests.FirstOrDefault(x => x.AssetId == LykkeConstants.BitcoinAssetId &&
                                                        x.TransferType == OffchainTransferType.HubCashout) != null;
                var hasLkkRequest =
                    currentRequests.FirstOrDefault(x => x.AssetId == LykkeConstants.LykkeAssetId &&
                                                        x.TransferType == OffchainTransferType.HubCashout) != null;

                var btcSetting = await GetAssetSetting(LykkeConstants.BitcoinAssetId);
                var lkkSetting = await GetAssetSetting(LykkeConstants.LykkeAssetId);

                var btcHubAmount = !currentChannels.HasError &&
                                   currentChannels.Balances.ContainsKey(LykkeConstants.BitcoinAssetId) &&
                                   currentChannels.Balances[LykkeConstants.BitcoinAssetId].Actual
                                    ? currentChannels.Balances[LykkeConstants.BitcoinAssetId].HubAmount
                                    : 0;

                var lkkHubAmount = !currentChannels.HasError &&
                                    currentChannels.Balances.ContainsKey(LykkeConstants.LykkeAssetId) &&
                                    currentChannels.Balances[LykkeConstants.LykkeAssetId].Actual
                                    ? currentChannels.Balances[LykkeConstants.LykkeAssetId].HubAmount
                                    : 0;

                var needBtcCashout = offchainTransfer.AssetId != LykkeConstants.BitcoinAssetId && btcHubAmount > btcSetting.Dust && !hasBtcRequest;
                var needLkkCashout = offchainTransfer.AssetId != LykkeConstants.LykkeAssetId && lkkHubAmount > lkkSetting.Dust && !hasLkkRequest;

                await _offchainRequestService.CreateHubCashoutRequests(offchainTransfer.ClientId, needBtcCashout ? btcHubAmount : 0, needLkkCashout ? lkkHubAmount : 0);
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(OffchainTransactionFinalizeFunction), nameof(CreateHubCashoutIfNeed), $"{offchainTransfer.ClientId}", e);
            }
        }

        private async Task<IAssetSetting> GetAssetSetting(string asset)
        {
            var setting = await _assetSettings.GetItemAsync(asset) ?? await _assetSettings.GetItemAsync(LykkeConstants.DefaultAssetSetting);
            if (setting == null)
                throw new Exception($"Setting is not found for {asset}");
            return setting;
        }
    }

    public class OffchainFinalizetionMessage
    {
        public string ClientId { get; set; }
        public string TransferId { get; set; }
        public string TransactionHash { get; set; }
    }
}
