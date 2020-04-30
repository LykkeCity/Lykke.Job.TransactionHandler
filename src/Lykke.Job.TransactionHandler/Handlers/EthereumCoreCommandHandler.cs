using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Services.Ethereum;
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
using Lykke.Job.EthereumCore.Contracts.Enums;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Events.EthereumCore;
using Lykke.Job.TransactionHandler.Commands.EthereumCore;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.MatchingEngine.Connector.Models.Api;
using Lykke.Service.ExchangeOperations.Client.Models;
using Lykke.Service.PersonalData.Contract;

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
        private readonly ITradeOperationsRepositoryClient _clientTradesRepositoryClient;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly ITransferOperationsRepositoryClient _transferEventsRepositoryClient;
        private readonly ITransactionService _transactionService;
        private readonly IAssetsService _assetsService;
        private readonly IEthererumPendingActionsRepository _ethererumPendingActionsRepository;
        private readonly IExchangeOperationsServiceClient _exchangeOperationsServiceClient;
        private readonly IClientCommentsRepository _clientCommentsRepository;
        private readonly IPersonalDataService _personalDataService;

        public EthereumCoreCommandHandler(
            ILogFactory logFactory,
            IMatchingEngineClient matchingEngineClient,
            ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IClientAccountClient clientAccountClient,
            ISrvEmailsFacade srvEmailsFacade,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            IPaymentTransactionsRepository paymentTransactionsRepository,
            ITradeOperationsRepositoryClient clientTradesRepositoryClient,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ITransferOperationsRepositoryClient transferEventsRepositoryClient,
            IAssetsServiceWithCache assetsServiceWithCache,
            ITransactionService transactionService,
            IAssetsService assetsService,
            IEthererumPendingActionsRepository ethererumPendingActionsRepository,
            IExchangeOperationsServiceClient exchangeOperationsServiceClient,
            IClientCommentsRepository clientCommentsRepository,
            IPersonalDataService personalDataService)
        {
            _log = logFactory.CreateLog(this);
            _matchingEngineClient = matchingEngineClient;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _clientAccountClient = clientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _paymentTransactionsRepository = paymentTransactionsRepository;
            _clientTradesRepositoryClient = clientTradesRepositoryClient;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _assetsServiceWithCache = assetsServiceWithCache;
            _transferEventsRepositoryClient = transferEventsRepositoryClient;
            _transactionService = transactionService;
            _assetsService = assetsService;
            _ethererumPendingActionsRepository = ethererumPendingActionsRepository;
            _exchangeOperationsServiceClient = exchangeOperationsServiceClient;
            _clientCommentsRepository = clientCommentsRepository;
            _personalDataService = personalDataService;
        }

        #region CommanHandling

        public async Task<CommandHandlingResult> Handle(ProcessHotWalletErc20EventCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                switch (command.EventType)
                {
                    case HotWalletEventType.CashinCompleted:
                        await ProcessHotWalletCashin(command, eventPublisher);
                        break;

                    case HotWalletEventType.CashoutCompleted:
                        await ProcessHotWalletCashout(command);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(
                            $"{command.EventType} - is not supported for processing {command.ToJson()}. ");
                }

                return CommandHandlingResult.Ok();
            }
            catch (Exception e)
            {
                _log.Error(nameof(ProcessHotWalletErc20EventCommand), e, context: command);
                throw;
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(EthereumCoreCommandHandler),  Command = nameof(ProcessHotWalletErc20EventCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }

        public async Task<CommandHandlingResult> Handle(ProcessEthCoinEventCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

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
                        await ProcessFailedCashout(command);
                        break;

                    case CoinEventType.CashinStarted:
                    case CoinEventType.CashoutStarted:
                    case CoinEventType.TransferStarted:
                        //DO NOTHING!
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(
                            $"{command.CoinEventType} - is not supported for processing {command.ToJson()}. ");
                }

                return CommandHandlingResult.Ok();
            }
            catch (Exception e)
            {
                _log.Error(nameof(ProcessEthCoinEventCommand), e, context: command);
                throw;
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(EthereumCoreCommandHandler),  Command = nameof(ProcessEthCoinEventCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }

        public async Task<CommandHandlingResult> Handle(EnrollEthCashinToMatchingEngineCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                var cashinId = command.CashinOperationId.ToString("N");
                var clientId = command.ClientId;
                var hash = command.TransactionHash;
                var amount = command.Amount;
                var asset = await _assetsServiceWithCache.TryGetAssetAsync(command.AssetId);
                var createPendingActions = command.CreatePendingActions;
                var paymentTransaction = PaymentTransaction.Create(hash,
                    CashInPaymentSystem.Ethereum, clientId.ToString(), (double) amount,
                    asset.DisplayId ?? asset.Id, status: PaymentStatus.Processing);

                var exists = await _paymentTransactionsRepository.CheckExistsAsync(paymentTransaction);

                if (exists)
                {
                    _log.Warning(command.TransactionHash ?? "Empty", $"Transaction already handled {hash}",
                        context: command);

                    return CommandHandlingResult.Ok();
                }

                if (createPendingActions && asset.IsTrusted)
                {
                    await _ethererumPendingActionsRepository.CreateAsync(clientId.ToString(),
                        Guid.NewGuid().ToString());
                }

                ChaosKitty.Meow();

                MeResponseModel result = null;

                await ExecuteWithTimeoutHelper.ExecuteWithTimeoutAsync(
                    async () =>
                    {
                        result = await _matchingEngineClient.CashInOutAsync(cashinId, clientId.ToString(), asset.Id,
                            (double) amount);
                    }, 5 * 60 * 1000); // 5 min in ms

                if (result == null ||
                    (result.Status != MeStatusCodes.Ok &&
                     result.Status != MeStatusCodes.Duplicate))
                {
                    _log.Warning(command.TransactionHash ?? "Empty", "ME error", context: result);

                    return CommandHandlingResult.Fail(TimeSpan.FromMinutes(1));
                }

                eventPublisher.PublishEvent(new EthCashinEnrolledToMatchingEngineEvent()
                {
                    TransactionHash = hash,
                });

                ChaosKitty.Meow();

                await _paymentTransactionsRepository.TryCreateAsync(paymentTransaction);

                return CommandHandlingResult.Ok();
            }
            catch (Exception e)
            {
                _log.Error(nameof(EnrollEthCashinToMatchingEngineCommand), e, context: command);
                throw;
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(EthereumCoreCommandHandler),  Command = nameof(EnrollEthCashinToMatchingEngineCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }

        public async Task<CommandHandlingResult> Handle(SaveEthInHistoryCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                var cashinId = command.CashinOperationId.ToString("N");
                var clientId = command.ClientId;
                var hash = command.TransactionHash;
                var amount = command.Amount;
                var clientAddress = command.ClientAddress;

                await _cashOperationsRepositoryClient.RegisterAsync(new CashInOutOperation
                {
                    Id = cashinId,
                    ClientId = clientId.ToString(),
                    AssetId = command.AssetId,
                    Amount = (double) amount,
                    BlockChainHash = hash,
                    DateTime = DateTime.UtcNow,
                    AddressTo = clientAddress,
                    State = TransactionStates.SettledOnchain
                });

                ChaosKitty.Meow();

                var clientAcc = await _clientAccountClient.GetByIdAsync(clientId.ToString());
                var clientEmail = await _personalDataService.GetEmailAsync(clientId.ToString());
                await _srvEmailsFacade.SendNoRefundDepositDoneMail(clientAcc.PartnerId, clientEmail, amount,
                    command.AssetId);
                await _paymentTransactionsRepository.SetStatus(hash, PaymentStatus.NotifyProcessed);

                ChaosKitty.Meow();

                eventPublisher.PublishEvent(new EthCashinSavedInHistoryEvent()
                {
                    TransactionHash = hash
                });

                return CommandHandlingResult.Ok();
            }
            catch (Exception e)
            {
                _log.Error(nameof(SaveEthInHistoryCommand), e, context: command);
                throw;
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(EthereumCoreCommandHandler),  Command = nameof(SaveEthInHistoryCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }

        #endregion

        #region Private

        private async Task ProcessHotWalletCashin(ProcessHotWalletErc20EventCommand queueMessage,
            IEventPublisher eventPublisher)
        {
            var bcnCreds = await _bcnClientCredentialsRepository.GetByAssetAddressAsync(queueMessage.FromAddress);
            string tokenAddress = queueMessage.TokenAddress;
            var token = await _assetsService.Erc20TokenGetByAddressAsync(tokenAddress);
            if (token == null)
            {
                _log.Error(new Exception($"Skipping cashin. Unsupported Erc 20 token - {tokenAddress}"),
                    context: queueMessage);

                return;
            }
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(token.AssetId);
            var amount = EthServiceHelpers.ConvertFromContract(queueMessage.Amount, asset.MultiplierPower, asset.Accuracy);
            Guid.TryParse(bcnCreds.ClientId, out var clientId);

            await HandleCashInOperation(asset, amount, clientId,
                bcnCreds.Address, queueMessage.TransactionHash, eventPublisher);
        }

        private async Task ProcessHotWalletCashout(ProcessHotWalletErc20EventCommand queueMessage)
        {
            string transactionId = queueMessage.OperationId;
            CashOutContextData context = await _transactionService.GetTransactionContext<CashOutContextData>(transactionId);

            if (context == null)
            {
                _log.Error(new NullReferenceException("Context is null for hotwallet cashout"),
                    context: queueMessage.ToJson());

                return;
            }

            string clientId = context.ClientId;
            string hash = queueMessage.TransactionHash;
            string cashOperationId = context.CashOperationId;

            var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);
            var clientEmail = await _personalDataService.GetEmailAsync(clientId);

            await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(clientId, cashOperationId, hash);
            await _srvEmailsFacade.SendNoRefundOCashOutMail(clientAcc.PartnerId, clientEmail, context.Amount, context.AssetId, hash);
        }

        private async Task ProcessOutcomeOperation(ProcessEthCoinEventCommand queueMessage)
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

                ChaosKitty.Meow();

                return;
            }

            if (context == null)
                return;

            string clientId = context.ClientId;
            string hash = queueMessage.TransactionHash;
            string cashOperationId = context.CashOperationId;

            var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);
            var clientEmail = await _personalDataService.GetEmailAsync(clientId);

            await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(clientId, cashOperationId, hash);
            await _srvEmailsFacade.SendNoRefundOCashOutMail(clientAcc.PartnerId, clientEmail, context.Amount, context.AssetId, hash);

            ChaosKitty.Meow();
        }

        //TODO: Split wth the help of the process management
        private async Task ProcessFailedCashout(ProcessEthCoinEventCommand queueMessage)
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
                    ChaosKitty.Meow();

                    var asset = await _assetsService.AssetGetAsync(assetId);
                    await _cashOperationsRepositoryClient.UpdateBlockchainHashAsync(clientId, cashOperationId, hash);
                    var pt = await _paymentTransactionsRepository.TryCreateAsync(PaymentTransaction.Create(hash,
                                CashInPaymentSystem.Ethereum, clientId, (double)amount,
                                asset.DisplayId ?? asset.Id, status: PaymentStatus.Processing));
                    if (pt == null)
                    {
                        _log.Warning($"{nameof(EthereumCoreCommandHandler)}:{nameof(ProcessFailedCashout)}", "Transaction already handled", context: hash);
                        return; //if was handled previously
                    }

                    ChaosKitty.Meow();

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

                    var exResult = await _exchangeOperationsServiceClient.ExchangeOperations.ManualCashInAsync(
                        new ManualCashInRequestModel
                        {
                            ClientId = clientId,
                            AssetId = assetId,
                            Amount = (double)amount,
                            UserId = "auto redeem",
                            Comment = commentText,
                        });

                    if (!exResult.IsOk())
                    {
                        _log.Warning($"{nameof(EthereumCoreCommandHandler)}:{nameof(ProcessFailedCashout)}",
                            "ME operation failed",
                            context: new {
                                ExchangeServiceResponse = exResult,
                                QueueMessage = queueMessage
                            }.ToJson());
                    }

                    await _clientCommentsRepository.AddClientCommentAsync(newComment);
                    await _paymentTransactionsRepository.SetStatus(hash, PaymentStatus.NotifyProcessed);

                    ChaosKitty.Meow();
                }
                catch (Exception e)
                {
                    _log.Error($"{nameof(EthereumCoreCommandHandler)}:{nameof(ProcessFailedCashout)}", e, context: queueMessage.ToJson());
                    throw;
                }
            }
            else
            {
                _log.Warning("Can't get a context", context: queueMessage.ToJson());
            }
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

        private async Task ProcessCashIn(ProcessEthCoinEventCommand queueMessage, IEventPublisher eventPublisher)
        {
            if (queueMessage.CoinEventType != CoinEventType.CashinCompleted)
                return;

            var bcnCreds = await _bcnClientCredentialsRepository.GetByAssetAddressAsync(queueMessage.FromAddress);
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(bcnCreds.AssetId);
            var amount = EthServiceHelpers.ConvertFromContract(queueMessage.Amount, asset.MultiplierPower, asset.Accuracy);
            Guid.TryParse(bcnCreds.ClientId, out var clientId);

            await HandleCashInOperation(asset, amount, clientId, bcnCreds.Address,
                queueMessage.TransactionHash, eventPublisher, createPendingActions: true);
        }

        private async Task HandleCashInOperation(Asset asset, decimal amount, Guid clientId, string clientAddress,
            string hash, IEventPublisher eventPublisher, bool createPendingActions = false)
        {
            var exists = await _paymentTransactionsRepository.CheckExistsAsync(PaymentTransaction.Create(hash,
                CashInPaymentSystem.Ethereum, clientId.ToString(), (double)amount,
                asset.DisplayId ?? asset.Id, status: PaymentStatus.Processing));

            if (exists)
            {
                return;
            }

            eventPublisher.PublishEvent(new CashinDetectedEvent
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
