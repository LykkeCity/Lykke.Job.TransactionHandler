using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Services.Ethereum;
using Lykke.MatchingEngine.Connector.Abstractions.Models;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.ExchangeOperations.Client;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.EthereumCore.Contracts.Events;
using Lykke.Job.EthereumCore.Contracts.Enums;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Events.EthereumCore;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Commands.EthereumCore;

namespace Lykke.Job.TransactionHandler.Handlers
{
    //Handler processes results from ethereum core completed events
    public class EthereumCoreCommandHandler
    {
        private readonly ILog _log;
        private readonly IMatchingEngineClient _matchingEngineClient;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IPaymentTransactionsRepository _paymentTransactionsRepository;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly ITradeOperationsRepositoryClient _clientTradesRepositoryClient;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly ITransferOperationsRepositoryClient _transferEventsRepositoryClient;
        private readonly ITransactionService _transactionService;
        private readonly IAssetsService _assetsService;
        private readonly IEthererumPendingActionsRepository _ethererumPendingActionsRepository;
        private readonly IDeduplicator _deduplicator;
        private readonly IExchangeOperationsServiceClient _exchangeOperationsServiceClient;
        private readonly IClientCommentsRepository _clientCommentsRepository;

        public EthereumCoreCommandHandler(AppSettings.RabbitMqSettings config, ILog log,
            IMatchingEngineClient matchingEngineClient,
            ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IClientAccountClient clientAccountClient,
            ISrvEmailsFacade srvEmailsFacade,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            IPaymentTransactionsRepository paymentTransactionsRepository,
            IWalletCredentialsRepository walletCredentialsRepository,
            ITradeOperationsRepositoryClient clientTradesRepositoryClient,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ITransferOperationsRepositoryClient transferEventsRepositoryClient,
            IAssetsServiceWithCache assetsServiceWithCache,
            ITransactionService transactionService,
            IAssetsService assetsService,
            IEthererumPendingActionsRepository ethererumPendingActionsRepository,
            IExchangeOperationsServiceClient exchangeOperationsServiceClient,
            IClientCommentsRepository clientCommentsRepository)
        {
            _log = log;
            _matchingEngineClient = matchingEngineClient;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _clientAccountClient = clientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _paymentTransactionsRepository = paymentTransactionsRepository;
            _walletCredentialsRepository = walletCredentialsRepository;
            _clientTradesRepositoryClient = clientTradesRepositoryClient;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _assetsServiceWithCache = assetsServiceWithCache;
            _transferEventsRepositoryClient = transferEventsRepositoryClient;
            _transactionService = transactionService;
            _assetsService = assetsService;
            _ethererumPendingActionsRepository = ethererumPendingActionsRepository;
            _exchangeOperationsServiceClient = exchangeOperationsServiceClient;
            _clientCommentsRepository = clientCommentsRepository;
        }

        #region CommanHandling

        public async Task<CommandHandlingResult> Handle(ProcessHotWalletEventCommand command, IEventPublisher eventPublisher)
        {
            try
            {
                switch (command.EventType)
                {
                    case EthereumCore.Contracts.Enums.HotWalletEventType.CashinCompleted:
                        await ProcessHotWalletCashin(command, eventPublisher);
                        break;

                    case EthereumCore.Contracts.Enums.HotWalletEventType.CashoutCompleted:
                        await ProcessHotWalletCashout(command);
                        break;

                    default:
                        break;
                }

                return CommandHandlingResult.Ok();
            }
            catch (Exception e)
            {
                _log.WriteError("EthereumCoreCommandHandler", command, e);
                throw;
            }
        }

        public async Task<CommandHandlingResult> Handle(ProcessCoinEventCommand command, IEventPublisher eventPublisher)
        {
            try
            {
                switch (command.CoinEventType)
                {
                    case CoinEventType.CashinCompleted:
                        await ProcessCashIn(command, eventPublisher);
                        break;
                    case CoinEventType.TransferCompleted:
                    case CoinEventType.CashoutCompleted:
                        await ProcessOutcomeOperation(command);
                        break;
                    case CoinEventType.CashoutFailed:
                        await ProcessFailedCashout(command, eventPublisher);
                        break;
                    default:
                        break; ;
                }

                return CommandHandlingResult.Ok();
            }
            catch (Exception e)
            {
                _log.WriteError("EthereumCoreCommandHandler", command, e);
                throw;
            }
}

        public async Task<CommandHandlingResult> Handle(EnrollEthCashinToMatchingEngineCommand command, IEventPublisher eventPublisher)
        {
            try
            {
                var cashinId = command.CashinOperationId.ToString("N");
                var clientId = command.ClientId;
                var hash = command.TransactionHash;
                var amount = command.Amount;
                var asset = await _assetsServiceWithCache.TryGetAssetAsync(command.AssetId);
                var createPendingActions = command.CreatePendingActions;
                var clientAddress = command.ClientAddress;
                var paymentTransaction = PaymentTransaction.Create(hash,
                    CashInPaymentSystem.Ethereum, clientId, (double)amount,
                    asset.DisplayId ?? asset.Id, status: PaymentStatus.Processing);

                var exists = await _paymentTransactionsRepository.CheckExistsAsync(paymentTransaction);

                if (exists)
                {
                    await
                        _log.WriteWarningAsync(nameof(EthereumCoreCommandHandler), nameof(Handle), command.ToJson(),
                            $"Transaction already handled {hash}");

                    return CommandHandlingResult.Ok();
                }

                if (createPendingActions)
                {
                    if (asset.IsTrusted)
                    {
                        await _ethererumPendingActionsRepository.CreateAsync(clientId, Guid.NewGuid().ToString());
                    }
                }

                var result = await _matchingEngineClient.CashInOutAsync(cashinId, clientId, asset.Id, (double)amount);

                if (result == null ||
                    result.Status != MeStatusCodes.Ok ||
                    result.Status != MeStatusCodes.Duplicate)
                {
                    await
                        _log.WriteWarningAsync(nameof(EthereumCoreCommandHandler), nameof(Handle), "ME error",
                            result.ToJson());

                    return CommandHandlingResult.Fail(TimeSpan.FromMinutes(5));
                }
                else
                {
                    await _paymentTransactionsRepository.TryCreateAsync(paymentTransaction);
                    eventPublisher.PublishEvent(new EthCashinEnrolledToMatchingEngineEvent()
                    {
                        CashinOperationId = command.CashinOperationId,
                        TransactionHash = hash,
                    });

                    return CommandHandlingResult.Ok();
                }
            }
            catch (Exception e)
            {
                _log.WriteError("EthereumCoreCommandHandler", command, e);
                throw;
            }
        }

        public async Task<CommandHandlingResult> Handle(SaveEthInHistoryCommand command, IEventPublisher eventPublisher)
        {
            try
            {
                var cashinId = command.CashinOperationId.ToString("N");
                var clientId = command.ClientId;
                var hash = command.TransactionHash;
                var amount = command.Amount;
                var asset = await _assetsServiceWithCache.TryGetAssetAsync(command.AssetId);
                var clientAddress = command.ClientAddress;

                var walletCreds = await _walletCredentialsRepository.GetAsync(clientId);
                await _cashOperationsRepositoryClient.RegisterAsync(new CashInOutOperation
                {
                    Id = cashinId,
                    ClientId = clientId,
                    Multisig = walletCreds.MultiSig,
                    AssetId = asset.Id,
                    Amount = (double)amount,
                    BlockChainHash = hash,
                    DateTime = DateTime.UtcNow,
                    AddressTo = clientAddress,
                    State = TransactionStates.SettledOnchain
                });

                var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);
                await _srvEmailsFacade.SendNoRefundDepositDoneMail(clientAcc.PartnerId, clientAcc.Email, amount, asset.Id);
                await _paymentTransactionsRepository.SetStatus(hash, PaymentStatus.NotifyProcessed);

                eventPublisher.PublishEvent(new EthCashinSavedInHistoryEvent()
                {
                    CashinOperationId = cashinId,
                    TransactionHash = hash
                });

                return CommandHandlingResult.Ok();
            }
            catch (Exception e)
            {
                _log.WriteError("EthereumCoreCommandHandler", command, e);
                throw;
            }
        }

        #endregion

        #region Private

        private async Task<bool> ProcessHotWalletCashin(ProcessHotWalletEventCommand queueMessage,
            IEventPublisher eventPublisher)
        {
            var bcnCreds = await _bcnClientCredentialsRepository.GetByAssetAddressAsync(queueMessage.FromAddress);
            string tokenAddress = queueMessage.TokenAddress;
            var token = await _assetsService.Erc20TokenGetByAddressAsync(tokenAddress);
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(token.AssetId);
            var amount = EthServiceHelpers.ConvertFromContract(queueMessage.Amount, asset.MultiplierPower, asset.Accuracy);

            await HandleCashInOperation(asset, amount, bcnCreds.ClientId,
                bcnCreds.Address, queueMessage.TransactionHash, eventPublisher);

            return true;
        }

        private async Task<bool> ProcessHotWalletCashout(ProcessHotWalletEventCommand queueMessage)
        {
            string transactionId = queueMessage.OperationId;
            CashOutContextData context = await _transactionService.GetTransactionContext<CashOutContextData>(transactionId);
            string clientId = context.ClientId;
            string hash = queueMessage.TransactionHash;
            string cashOperationId = context.CashOperationId;

            await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(clientId, cashOperationId, hash);

            return true;
        }

        private async Task<bool> ProcessOutcomeOperation(ProcessCoinEventCommand queueMessage)
        {
            var transferTx = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(queueMessage.OperationId));
            CashOutContextData context = await _transactionService.GetTransactionContext<CashOutContextData>(queueMessage.OperationId);


            if (transferTx != null)
            {
                switch (transferTx.OperationType)
                {
                    case OperationType.CashOut:
                        await SetCashoutHashes(transferTx, queueMessage.TransactionHash);
                        break;
                    case OperationType.Trade:
                        await SetTradeHashes(transferTx, queueMessage.TransactionHash);
                        break;
                    case OperationType.TransferToTrusted:
                    case OperationType.TransferFromTrusted:
                        await SetTransferHashes(transferTx, queueMessage.TransactionHash);
                        break;
                }

                return true;
            }

            if (context != null)
            {
                string clientId = context.ClientId;
                string hash = queueMessage.TransactionHash;
                string cashOperationId = context.CashOperationId;

                await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(clientId, cashOperationId, hash);
            }


            return true;
        }

        //TODO: Split wth the help of the process management
        private async Task<bool> ProcessFailedCashout(ProcessCoinEventCommand queueMessage, IEventPublisher eventPublisher)
        {
            CashOutContextData context = await _transactionService.GetTransactionContext<CashOutContextData>(queueMessage.OperationId);

            if (context != null)
            {
                string transactionHandlerUserId = "auto redeem";
                string clientId = context.ClientId;
                string hash = queueMessage.TransactionHash;
                string cashOperationId = context.CashOperationId;
                string assetId = context.AssetId;
                var amount = context.Amount;

                try
                {
                    var asset = await _assetsService.AssetGetAsync(assetId);
                    await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(clientId, cashOperationId, hash);
                    var pt = await _paymentTransactionsRepository.TryCreateAsync(PaymentTransaction.Create(hash,
                                CashInPaymentSystem.Ethereum, clientId, (double)amount,
                                asset.DisplayId ?? asset.Id, status: PaymentStatus.Processing));
                    if (pt == null)
                    {
                        await
                            _log.WriteWarningAsync(nameof(EthereumCoreCommandHandler), nameof(ProcessFailedCashout), hash,
                                "Transaction already handled");
                        //return if was handled previously
                        return false;
                    }

                    var sign = "+";
                    var commentText =
                        $"Balance Update: {sign}{amount} {asset.Name}. Cashout failed: {hash} {queueMessage.OperationId}";

                    var newComment = new ClientComment
                    {
                        ClientId = clientId,
                        Comment = commentText,
                        CreatedAt = DateTime.UtcNow,
                        FullName = "Lykke.Job.TransactionHandler",
                        UserId = transactionHandlerUserId
                    };

                    var exResult = await _exchangeOperationsServiceClient.ManualCashInAsync(clientId, assetId,
                            (double)amount, "auto redeem", commentText);

                    if (!exResult.IsOk())
                    {
                        await
                            _log.WriteWarningAsync(nameof(EthereumCoreCommandHandler), nameof(ProcessFailedCashout),
                            (new
                            {
                                ExchangeServiceResponse = exResult,
                                QueueMessage = queueMessage
                            }).ToJson(),
                                "ME operation failed");

                        return false;
                    }

                    await _clientCommentsRepository.AddClientCommentAsync(newComment);
                    await _paymentTransactionsRepository.SetStatus(hash, PaymentStatus.NotifyProcessed);
                }
                catch (Exception e)
                {
                    await _log.WriteErrorAsync(nameof(EthereumCoreCommandHandler), nameof(ProcessFailedCashout), queueMessage.ToJson(), e);
                    throw e;
                }
            }
            else
            {
                await _log.WriteWarningAsync(nameof(EthereumCoreCommandHandler), nameof(ProcessFailedCashout), queueMessage.ToJson());
            }

            return true;
        }

        private async Task SetTradeHashes(IEthereumTransactionRequest txRequest, string hash)
        {
            foreach (var id in txRequest.OperationIds)
            {
                await _clientTradesRepositoryClient.UpdateBlockchainHashAsync(txRequest.ClientId, id, hash);
            }
        }

        private async Task SetCashoutHashes(IEthereumTransactionRequest txRequest, string hash)
        {
            foreach (var id in txRequest.OperationIds)
            {
                await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(txRequest.ClientId, id, hash);
            }
        }

        private async Task SetTransferHashes(IEthereumTransactionRequest txRequest, string hash)
        {
            foreach (var id in txRequest.OperationIds)
            {
                await _transferEventsRepositoryClient.UpdateBlockChainHashAsync(txRequest.ClientId, id, hash);
            }
        }

        private async Task<bool> ProcessCashIn(ProcessCoinEventCommand queueMessage, IEventPublisher eventPublisher)
        {
            if (queueMessage.CoinEventType != CoinEventType.CashinCompleted)
                return true;

            var bcnCreds = await _bcnClientCredentialsRepository.GetByAssetAddressAsync(queueMessage.FromAddress);
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(bcnCreds.AssetId);
            var amount = EthServiceHelpers.ConvertFromContract(queueMessage.Amount, asset.MultiplierPower, asset.Accuracy);

            await HandleCashInOperation(asset, amount, bcnCreds.ClientId, bcnCreds.Address,
                queueMessage.TransactionHash, eventPublisher, createPendingActions: true);

            return true;
        }

        private async Task HandleCashInOperation(Asset asset, decimal amount, string clientId, string clientAddress,
            string hash, IEventPublisher eventPublisher, bool createPendingActions = false)
        {
            var exists = await _paymentTransactionsRepository.CheckExistsAsync(PaymentTransaction.Create(hash,
                CashInPaymentSystem.Ethereum, clientId, (double)amount,
                asset.DisplayId ?? asset.Id, status: PaymentStatus.Processing));

            if (exists)
            {
                return;
            }

            eventPublisher.PublishEvent(new CashinDetectedEvent()
            {
                ClientId = clientId,
                ClientAddress = clientAddress,
                AssetId = asset.Id,
                Amount = amount,
                TransactionHash = hash,
                CreatePendingActions = createPendingActions
            });
        }

        #endregion
    }
}
