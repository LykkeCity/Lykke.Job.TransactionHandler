using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Bitcoin.Api.Client.BitcoinApi;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.ChronoBank;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class CashInOutQueue : IQueueSubscriber
    {
        private const string QueueName = "transactions.cashinout";

        private readonly ILog _log;
        private readonly IBitcoinCommandSender _bitcoinCommandSender;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionsRepository;
        private readonly IForwardWithdrawalRepository _forwardWithdrawalRepository;
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly ISrvEthereumHelper _srvEthereumHelper;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IEthClientEventLogs _ethClientEventLogs;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly AppSettings.EthereumSettings _settings;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly ISrvSolarCoinHelper _srvSolarCoinHelper;
        private readonly IChronoBankService _chronoBankService;
        private readonly IBitcoinApiClient _bitcoinApiClient;
        private readonly IDeduplicator _deduplicator;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<CashInOutQueueMessage> _subscriber;

        public CashInOutQueue(AppSettings.RabbitMqSettings config, ILog log,
            IBitcoinCommandSender bitcoinCommandSender,
            ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            IWalletCredentialsRepository walletCredentialsRepository,
            IBitCoinTransactionsRepository bitcoinTransactionsRepository,
            IForwardWithdrawalRepository forwardWithdrawalRepository,
            IOffchainRequestService offchainRequestService,
            IClientSettingsRepository clientSettingsRepository,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ISrvEthereumHelper srvEthereumHelper,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            IEthClientEventLogs ethClientEventLogs,
            IBitcoinTransactionService bitcoinTransactionService,
            IAssetsServiceWithCache assetsServiceWithCache,
            AppSettings.EthereumSettings settings,
            IClientAccountClient clientAccountClient,
            ISrvEmailsFacade srvEmailsFacade,
            ISrvSolarCoinHelper srvSolarCoinHelper,
            IChronoBankService chronoBankService,
            IBitcoinApiClient bitcoinApiClient,
            [NotNull] IDeduplicator deduplicator)
        {
            _rabbitConfig = config;
            _log = log;
            _bitcoinCommandSender = bitcoinCommandSender;
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient;
            _walletCredentialsRepository = walletCredentialsRepository;
            _bitcoinTransactionsRepository = bitcoinTransactionsRepository;
            _forwardWithdrawalRepository = forwardWithdrawalRepository;
            _offchainRequestService = offchainRequestService;
            _clientSettingsRepository = clientSettingsRepository;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _srvEthereumHelper = srvEthereumHelper;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _ethClientEventLogs = ethClientEventLogs;
            _bitcoinTransactionService = bitcoinTransactionService;
            _assetsServiceWithCache = assetsServiceWithCache;
            _settings = settings;
            _clientAccountClient = clientAccountClient;
            _srvEmailsFacade = srvEmailsFacade;
            _srvSolarCoinHelper = srvSolarCoinHelper;
            _chronoBankService = chronoBankService;
            _bitcoinApiClient = bitcoinApiClient;
            _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = QueueName,
                ExchangeName = _rabbitConfig.ExchangeCashOperation,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeCashOperation}.dlx",
                RoutingKey = "",
                IsDurable = true
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<CashInOutQueueMessage>(settings, new DeadQueueErrorHandlingStrategy(_log, settings))
                    .SetMessageDeserializer(new JsonMessageDeserializer<CashInOutQueueMessage>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(CashInOutQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        public async Task<bool> ProcessMessage(CashInOutQueueMessage queueMessage)
        {
            if (!await _deduplicator.EnsureNotDuplicateAsync(queueMessage))
            {
                await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), "Duplicated message");
                return false;
            }

            var transaction = await _bitcoinTransactionsRepository.FindByTransactionIdAsync(queueMessage.Id);
            if (transaction == null)
            {
                // external cashin
                if (_cashOperationsRepositoryClient.GetAsync(queueMessage.ClientId, queueMessage.Id) != null)
                    return await ProcessExternalCashin(queueMessage);

                await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), "unkown transaction");
                return false;
            }

            try
            {
                switch (transaction.CommandType)
                {
                    case BitCoinCommands.CashIn:
                    case BitCoinCommands.Issue:
                        return await ProcessIssue(transaction, queueMessage);
                    case BitCoinCommands.CashOut:
                        return await ProcessCashOut(transaction, queueMessage);
                    case BitCoinCommands.Destroy:
                        return await ProcessDestroy(transaction, queueMessage);
                    case BitCoinCommands.ManualUpdate:
                        return await ProcessManualOperation(transaction, queueMessage);
                    default:
                        await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), $"Unknown command type (value = [{transaction.CommandType}])");
                        return false;
                }
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(nameof(CashInOutQueue), nameof(ProcessMessage), queueMessage.ToJson(), ex);
                return false;
            }
        }

        private async Task<bool> ProcessDestroy(IBitcoinTransaction transaction, CashInOutQueueMessage msg)
        {
            var amount = msg.Amount.ParseAnyDouble();
            //Get uncolor context data
            var context = await _bitcoinTransactionService.GetTransactionContext<UncolorContextData>(transaction.TransactionId);

            //Register cash operation
            var cashOperationId = await _cashOperationsRepositoryClient
                .RegisterAsync(new CashInOutOperation
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ClientId = msg.ClientId,
                    Multisig = context.AddressFrom,
                    AssetId = msg.AssetId,
                    Amount = -Math.Abs(amount),
                    DateTime = DateTime.UtcNow,
                    AddressFrom = context.AddressFrom,
                    AddressTo = context.AddressTo,
                    TransactionId = msg.Id
                });

            //Update context data
            context.CashOperationId = cashOperationId;
            var contextJson = context.ToJson();
            var cmd = new DestroyCommand
            {
                Context = contextJson,
                Amount = Math.Abs(amount),
                AssetId = msg.AssetId,
                Address = context.AddressFrom,
                TransactionId = Guid.Parse(msg.Id)
            };

            await _bitcoinTransactionsRepository.UpdateAsync(transaction.TransactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transaction.TransactionId, context);

            //Send to bitcoin
            await _bitcoinCommandSender.SendCommand(cmd);

            return true;
        }

        private async Task<bool> ProcessManualOperation(IBitcoinTransaction transaction, CashInOutQueueMessage msg)
        {
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(msg.AssetId);
            var walletCredentials = await _walletCredentialsRepository.GetAsync(msg.ClientId);
            var context = transaction.GetContextData<CashOutContextData>();
            var isOffchainClient = await _clientSettingsRepository.IsOffchainClient(msg.ClientId);
            var isBtcOffchainClient = isOffchainClient && asset.Blockchain == Blockchain.Bitcoin;

            var operation = new CashInOutOperation
            {
                Id = Guid.NewGuid().ToString(),
                ClientId = msg.ClientId,
                Multisig = walletCredentials.MultiSig,
                AssetId = msg.AssetId,
                Amount = msg.Amount.ParseAnyDouble(),
                DateTime = DateTime.UtcNow,
                AddressFrom = walletCredentials.MultiSig,
                AddressTo = context.Address,
                TransactionId = msg.Id,
                Type = CashOperationType.None,
                BlockChainHash = asset.IssueAllowed && isBtcOffchainClient ? string.Empty : transaction.BlockchainHash,
                State = GetState(transaction, isBtcOffchainClient)
            };

            try
            {
                await _cashOperationsRepositoryClient.RegisterAsync(operation);
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(CashInOutQueue), nameof(ProcessManualOperation), null, e);
                return false;
            }

            return true;
        }

        private async Task<bool> ProcessCashOut(IBitcoinTransaction transaction, CashInOutQueueMessage msg)
        {
            //Get client wallet
            var walletCredentials = await _walletCredentialsRepository
                .GetAsync(msg.ClientId);
            var amount = msg.Amount.ParseAnyDouble();
            var context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transaction.TransactionId);

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(msg.AssetId);

            var isOffchainClient = await _clientSettingsRepository.IsOffchainClient(msg.ClientId);
            var isBtcOffchainClient = isOffchainClient && asset.Blockchain == Blockchain.Bitcoin;

            bool isForwardWithdawal = context.AddData?.ForwardWithdrawal != null;
            if (isForwardWithdawal)
            {
                var baseAsset = await _assetsServiceWithCache.TryGetAssetAsync(asset.ForwardBaseAsset);

                var forwardCashInId = await _cashOperationsRepositoryClient
                    .RegisterAsync(new CashInOutOperation
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClientId = msg.ClientId,
                        Multisig = walletCredentials.MultiSig,
                        AssetId = baseAsset.Id,
                        Amount = Math.Abs(amount),
                        DateTime = DateTime.UtcNow.AddDays(asset.ForwardFrozenDays),
                        AddressFrom = walletCredentials.MultiSig,
                        AddressTo = context.Address,
                        TransactionId = msg.Id,
                        Type = CashOperationType.ForwardCashIn,
                        State = isBtcOffchainClient ? TransactionStates.InProcessOffchain : TransactionStates.InProcessOnchain
                    });

                await _forwardWithdrawalRepository.SetLinkedCashInOperationId(msg.ClientId, context.AddData.ForwardWithdrawal.Id,
                    forwardCashInId);
            }

            //Register cash operation
            string cashOperationId = await _cashOperationsRepositoryClient
                .RegisterAsync(new CashInOutOperation
                {
                    Id = Guid.NewGuid().ToString(),
                    ClientId = msg.ClientId,
                    Multisig = walletCredentials.MultiSig,
                    AssetId = msg.AssetId,
                    Amount = -Math.Abs(amount),
                    DateTime = DateTime.UtcNow,
                    AddressFrom = walletCredentials.MultiSig,
                    AddressTo = context.Address,
                    TransactionId = msg.Id,
                    Type = isForwardWithdawal ? CashOperationType.ForwardCashOut : CashOperationType.None,
                    BlockChainHash = asset.IssueAllowed && isBtcOffchainClient ? string.Empty : transaction.BlockchainHash,
                    State = isForwardWithdawal ? TransactionStates.SettledOffchain : GetState(transaction, isBtcOffchainClient)
                });

            //Update context data
            context.CashOperationId = cashOperationId;
            var contextJson = context.ToJson();
            var cmd = new CashOutCommand
            {
                Amount = Math.Abs(amount),
                AssetId = msg.AssetId,
                Context = contextJson,
                SourceAddress = walletCredentials.MultiSig,
                DestinationAddress = context.Address,
                TransactionId = Guid.Parse(transaction.TransactionId)
            };

            await _bitcoinTransactionsRepository.UpdateAsync(transaction.TransactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transaction.TransactionId, context);

            if (asset.Blockchain == Blockchain.Ethereum)
            {
                string errMsg = string.Empty;

                if (asset.Type == AssetType.Erc20Token)
                {
                    try
                    {
                        var response = await _srvEthereumHelper.HotWalletCashoutAsync(transaction.TransactionId,
                            _settings.HotwalletAddress,
                            context.Address,
                            (decimal)Math.Abs(amount),
                            asset);

                        if (response.HasError)
                            errMsg = response.Error.ToJson();
                    }
                    catch (Exception e)
                    {
                        errMsg = $"{e.GetType()}\n{e.Message}";
                    }
                }
                else
                {
                    try
                    {
                        if (!asset.IsTrusted)
                        {

                            var address = await _bcnClientCredentialsRepository.GetClientAddress(msg.ClientId);
                            var txRequest =
                                await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(transaction.TransactionId));

                            txRequest.OperationIds = new[] { cashOperationId };
                            await _ethereumTransactionRequestRepository.UpdateAsync(txRequest);

                            var response = await _srvEthereumHelper.SendCashOutAsync(txRequest.Id,
                                txRequest.SignedTransfer.Sign,
                                asset, address, txRequest.AddressTo,
                                txRequest.Volume);

                            if (response.HasError)
                                errMsg = response.Error.ToJson();
                        }
                        else
                        {
                            var txId = Guid.Parse(transaction.TransactionId);
                            var address = _settings.HotwalletAddress;

                            var response = await _srvEthereumHelper.SendCashOutAsync(txId,
                                "",
                                asset, address, context.Address,
                                (decimal)Math.Abs(amount));

                            if (response.HasError)
                                errMsg = response.Error.ToJson();
                        }
                    }
                    catch (Exception e)
                    {
                        errMsg = $"{e.GetType()}\n{e.Message}";
                    }
                }

                if (!string.IsNullOrEmpty(errMsg))
                {
                    await _ethClientEventLogs.WriteEvent(msg.ClientId, Event.Error,
                        new { Request = transaction.TransactionId, Error = errMsg }.ToJson());
                }
            }

            if (asset.Id == LykkeConstants.SolarAssetId)
                await ProcessSolarCashOut(msg.ClientId, context.Address, Math.Abs(amount), transaction.TransactionId);

            if (asset.Id == LykkeConstants.ChronoBankAssetId)
                await ProcessChronoBankCashOut(context.Address, Math.Abs(amount), transaction.TransactionId);

            if (asset.Blockchain == Blockchain.Bitcoin && asset.IsTrusted && asset.BlockchainWithdrawal && !isForwardWithdawal)
                await ProcessBitcoinCashOut(asset, context.Address, (decimal)Math.Abs(amount), transaction.TransactionId);

            return true;
        }

        private static TransactionStates GetState(
            IBitcoinTransaction transaction, bool isBtcOffchainClient)
        {
            return isBtcOffchainClient
                ? (string.IsNullOrWhiteSpace(transaction.BlockchainHash)
                    ? TransactionStates.SettledOffchain
                    : TransactionStates.SettledOnchain)
                : (string.IsNullOrWhiteSpace(transaction.BlockchainHash)
                    ? TransactionStates.InProcessOnchain
                    : TransactionStates.SettledOnchain);
        }

        private async Task<bool> ProcessIssue(IBitcoinTransaction transaction, CashInOutQueueMessage msg)
        {
            var isOffchain = await _clientSettingsRepository.IsOffchainClient(msg.ClientId);

            //Get client wallet
            var walletCredentials = await _walletCredentialsRepository
                .GetAsync(msg.ClientId);
            var amount = msg.Amount.ParseAnyDouble();
            var context = await _bitcoinTransactionService.GetTransactionContext<IssueContextData>(transaction.TransactionId);
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(msg.AssetId);

            //Register cash operation
            var cashOperationId = await _cashOperationsRepositoryClient
                .RegisterAsync(new CashInOutOperation
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ClientId = msg.ClientId,
                    Multisig = walletCredentials.MultiSig,
                    AssetId = msg.AssetId,
                    Amount = Math.Abs(amount),
                    DateTime = DateTime.UtcNow,
                    AddressTo = walletCredentials.MultiSig,
                    TransactionId = transaction.TransactionId,
                    State = asset.IsTrusted ? TransactionStates.SettledOffchain : TransactionStates.InProcessOffchain
                });

            context.CashOperationId = cashOperationId;
            var contextJson = context.ToJson();
            var cmd = new IssueCommand
            {
                Amount = amount,
                AssetId = msg.AssetId,
                Multisig = walletCredentials.MultiSig,
                Context = contextJson,
                TransactionId = Guid.Parse(transaction.TransactionId)
            };

            await _bitcoinTransactionsRepository.UpdateAsync(transaction.TransactionId, cmd.ToJson(), null, "");

            await _bitcoinTransactionService.SetTransactionContext(transaction.TransactionId, context);

            var isClientTrusted = await _clientAccountClient.IsTrustedAsync(msg.ClientId);

            if (isClientTrusted.Value || asset.IsTrusted)
                return true;

            if (isOffchain)
                await _offchainRequestService.CreateOffchainRequestAndNotify(transaction.TransactionId, msg.ClientId, msg.AssetId, (decimal)amount, null, OffchainTransferType.CashinToClient);
            else
                await _bitcoinCommandSender.SendCommand(cmd);

            return true;
        }

        private async Task<bool> ProcessExternalCashin(CashInOutQueueMessage msg)
        {
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(msg.AssetId);
            if (!await _clientSettingsRepository.IsOffchainClient(msg.ClientId) || asset.Blockchain != Blockchain.Bitcoin || asset.IsTrusted && asset.Id != LykkeConstants.BitcoinAssetId)
                return true;

            if (asset.Id == LykkeConstants.BitcoinAssetId)
            {
                var amount = msg.Amount.ParseAnyDouble();
                await _offchainRequestService.CreateOffchainRequestAndNotify(Guid.NewGuid().ToString(), msg.ClientId, msg.AssetId, (decimal)amount, null, OffchainTransferType.TrustedCashout);
            }

            return true;
        }

        private Task ProcessChronoBankCashOut(string address, double amount, string txId)
        {
            return _chronoBankService.SendCashOutRequest(txId, address, amount);
        }

        private async Task ProcessSolarCashOut(string clientId, string address, double amount, string txId)
        {
            var slrAddress = new SolarCoinAddress(address);
            var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);

            var sendEmailTask = _srvEmailsFacade.SendSolarCashOutCompletedEmail(clientAcc.PartnerId, clientAcc.Email, slrAddress.Value, amount);
            var solarRequestTask = _srvSolarCoinHelper.SendCashOutRequest(txId, slrAddress, amount);

            await Task.WhenAll(sendEmailTask, solarRequestTask);
        }

        private Task ProcessBitcoinCashOut(Asset asset, string address, decimal amount, string transactionId)
        {
            return _bitcoinApiClient.CashoutAsync(new Bitcoin.Api.Client.BitcoinApi.Models.CashoutModel
            {
                Amount = amount,
                AssetId = asset.Id,
                DestinationAddress = address,
                TransactionId = Guid.Parse(transactionId)
            });
        }

        public void Dispose()
        {
            Stop();
        }
    }
}