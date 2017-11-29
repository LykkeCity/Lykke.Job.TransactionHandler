using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;

namespace Lykke.Job.TransactionHandler.Queues
{
    public class TradeQueue : IQueueSubscriber
    {
#if DEBUG
        private const string QueueName = "transactions.trades-dev";
        private const bool QueueDurable = false;
#else
        private const string QueueName = "transactions.trades";
        private const bool QueueDurable = true;
#endif

        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionsRepository;
        private readonly IMarketOrdersRepository _marketOrdersRepository;
        private readonly IClientTradesRepository _clientTradesRepository;
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IOffchainOrdersRepository _offchainOrdersRepository;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly ISrvEthereumHelper _srvEthereumHelper;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly AppSettings.EthereumSettings _settings;
        private readonly IEthClientEventLogs _ethClientEventLogs;
        private readonly ILog _log;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IFeeLogRepository _feeLogRepository;

        private readonly AppSettings.RabbitMqSettings _rabbitConfig;
        private RabbitMqSubscriber<TradeQueueItem> _subscriber;

        public TradeQueue(
            AppSettings.RabbitMqSettings config,
            ILog log,
            IWalletCredentialsRepository walletCredentialsRepository,
            IBitCoinTransactionsRepository bitcoinTransactionsRepository,
            IMarketOrdersRepository marketOrdersRepository,
            IClientTradesRepository clientTradesRepository,
            IOffchainRequestService offchainRequestService,
            IOffchainOrdersRepository offchainOrdersRepository,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            ISrvEthereumHelper srvEthereumHelper,
            IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            AppSettings.EthereumSettings settings,
            IEthClientEventLogs ethClientEventLogs, IBitcoinTransactionService bitcoinTransactionService,
            IClientAccountClient clientAccountClient, IAssetsServiceWithCache assetsServiceWithCache,
            IFeeLogRepository feeLogRepository)
        {
            _rabbitConfig = config;
            _walletCredentialsRepository = walletCredentialsRepository;
            _bitcoinTransactionsRepository = bitcoinTransactionsRepository;
            _marketOrdersRepository = marketOrdersRepository;
            _clientTradesRepository = clientTradesRepository;
            _offchainRequestService = offchainRequestService;
            _offchainOrdersRepository = offchainOrdersRepository;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _srvEthereumHelper = srvEthereumHelper;
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository;
            _settings = settings;
            _ethClientEventLogs = ethClientEventLogs;
            _bitcoinTransactionService = bitcoinTransactionService;
            _clientAccountClient = clientAccountClient;
            _assetsServiceWithCache = assetsServiceWithCache;
            _log = log;
            _feeLogRepository = feeLogRepository;
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _rabbitConfig.ConnectionString,
                QueueName = QueueName,
                ExchangeName = _rabbitConfig.ExchangeSwap,
                DeadLetterExchangeName = $"{_rabbitConfig.ExchangeSwap}.dlx",
                RoutingKey = "",
                IsDurable = QueueDurable
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<TradeQueueItem>(settings, new DeadQueueErrorHandlingStrategy(_log, settings))
                    .SetMessageDeserializer(new JsonMessageDeserializer<TradeQueueItem>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessMessage)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(TradeQueue), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }

        public async Task ProcessMessage(TradeQueueItem queueMessage)
        {
            await _marketOrdersRepository.CreateAsync(queueMessage.Order);

            var feeLogTasks = queueMessage.Trades.Select(ti => _feeLogRepository.CreateAsync(new OrderFeeLog
            {
                OrderId = queueMessage.Order.Id,
                OrderStatus = queueMessage.Order.Status,
                FeeInstruction = ti.FeeInstruction?.ToJson(),
                FeeTransfer = ti.FeeTransfer?.ToJson(),
                Type = "market"
            }));
            await Task.WhenAll(feeLogTasks);

            if (!queueMessage.Order.Status.Equals("matched", StringComparison.OrdinalIgnoreCase))
            {
                await _log.WriteInfoAsync(nameof(TradeQueue), nameof(ProcessMessage), queueMessage.Order.ToJson(), "Message processing being aborted, due to order status is not matched. Order was saved");
                return;
            }

            var walletCredsMarket = await _walletCredentialsRepository.GetAsync(queueMessage.Trades[0].MarketClientId);
            var walletCredsLimit = await _walletCredentialsRepository.GetAsync(queueMessage.Trades[0].LimitClientId);

            var clientTrades = queueMessage.ToDomainOffchain(walletCredsMarket, walletCredsLimit, await _assetsServiceWithCache.GetAllAssetsAsync());
            
            var notify = new HashSet<string>();
            try
            {
                // for trusted clients only write history (finally block)
                if ((await _clientAccountClient.IsTrustedAsync(queueMessage.Order.ClientId)).Value)
                    return;

                // get operations only by market order user (limit user will be processed in limit trade queue)
                var operations = AggregateSwaps(queueMessage.Trades).Where(x => x.ClientId == queueMessage.Order.ClientId).ToList();

                await CreateTransaction(queueMessage.Order.Id, operations, clientTrades);

                var ethereumTxRequest = await _ethereumTransactionRequestRepository.GetByOrderAsync(queueMessage.Order.Id);

                if (ethereumTxRequest != null)
                {
                    var wasTransferOk = await ProcessEthGuaranteeTransfer(ethereumTxRequest, operations, clientTrades);

                    if (!wasTransferOk)
                        return;
                }

                var sellOperations = operations.Where(x => x.Amount < 0);
                var buyOperations = operations.Where(x => x.Amount > 0);

                foreach (var operation in sellOperations)
                {
                    var asset = await _assetsServiceWithCache.TryGetAssetAsync(operation.AssetId);
                    if (asset.Blockchain == Blockchain.Ethereum || asset.IsTrusted)
                        continue;   //guarantee transfer already sent for eth

                    // return change in offchain
                    var offchainOrder = await _offchainOrdersRepository.GetOrder(queueMessage.Order.Id);

                    var change = offchainOrder.ReservedVolume - Math.Abs(operation.Amount);

                    if (change < 0)
                        await _log.WriteWarningAsync(nameof(TradeQueue), nameof(ProcessMessage),
                            $"Order: [{offchainOrder.OrderId}], data: [{operation.ToJson()}]",
                            "Diff is less than ZERO !");

                    if (change > 0)
                    {
                        await _offchainRequestService.CreateOffchainRequestAndNotify(operation.TransferId, operation.ClientId,
                            operation.AssetId, change, offchainOrder.OrderId, OffchainTransferType.FromHub);
                        notify.Add(operation.ClientId);
                    }
                }

                foreach (var operation in buyOperations)
                {
                    var asset = await _assetsServiceWithCache.TryGetAssetAsync(operation.AssetId);

                    if (asset.IsTrusted)
                        continue;

                    if (asset.Blockchain == Blockchain.Ethereum)
                    {
                        await ProcessEthBuy(operation, asset, clientTrades, queueMessage.Order.Id);
                        continue;
                    }

                    await _offchainRequestService.CreateOffchainRequestAndNotify(operation.TransferId, operation.ClientId,
                        operation.AssetId, operation.Amount, queueMessage.Order.Id, OffchainTransferType.FromHub);
                    notify.Add(operation.ClientId);
                }
            }
            finally
            {
                await _clientTradesRepository.SaveAsync(clientTrades);

                foreach (var item in notify)
                    await _offchainRequestService.NotifyUser(item);
            }
        }

        private async Task ProcessEthBuy(AggregatedTransfer operation, Asset asset, IClientTrade[] clientTrades, string orderId)
        {
            string errMsg = string.Empty;
            var transferId = Guid.NewGuid();

            try
            {
                var toAddress = await _bcnClientCredentialsRepository.GetClientAddress(operation.ClientId);

                await _ethereumTransactionRequestRepository.InsertAsync(new EthereumTransactionRequest
                {
                    AddressTo = toAddress,
                    AssetId = asset.Id,
                    ClientId = operation.ClientId,
                    Id = transferId,
                    OperationIds =
                        clientTrades.Where(x => x.ClientId == operation.ClientId && x.Amount > 0)
                            .Select(x => x.Id)
                            .ToArray(),
                    OperationType = OperationType.Trade,
                    OrderId = orderId,
                    Volume = operation.Amount
                });

                var res = await _srvEthereumHelper.SendTransferAsync(transferId, string.Empty, asset,
                    _settings.HotwalletAddress, toAddress, operation.Amount);

                if (res.HasError)
                {
                    errMsg = res.Error.ToJson();

                    await _log.WriteWarningAsync(nameof(TradeQueue), nameof(ProcessEthGuaranteeTransfer), errMsg, string.Empty);
                }
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(TradeQueue), nameof(ProcessEthGuaranteeTransfer), e.Message, e);

                errMsg = $"{e.GetType()}\n{e.Message}";
            }

            if (!string.IsNullOrEmpty(errMsg))
            {
                await _ethClientEventLogs.WriteEvent(operation.ClientId, Event.Error, new
                {
                    Info = $"{asset.Id} was not transferred to client",
                    RequestId = transferId,
                    Operation = operation.ToJson(),
                    Error = errMsg
                }.ToJson());
            }
        }

        private async Task<bool> ProcessEthGuaranteeTransfer(IEthereumTransactionRequest ethereumTxRequest, List<AggregatedTransfer> operations, IClientTrade[] clientTrades)
        {
            var errMsg = string.Empty;
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(ethereumTxRequest.AssetId);
            try
            {
                var fromAddress = await _bcnClientCredentialsRepository.GetClientAddress(ethereumTxRequest.ClientId);
                var clientEthSellOperation =
                    operations.First(x => x.Amount < 0 && x.ClientId == ethereumTxRequest.ClientId);
                var change = ethereumTxRequest.Volume - Math.Abs(clientEthSellOperation.Amount);

                EthereumResponse<OperationResponse> res;
                var minAmountForAsset = (decimal)Math.Pow(10, -asset.Accuracy);
                if (change > 0 && Math.Abs(change) >= minAmountForAsset)
                {
                    res = await _srvEthereumHelper.SendTransferWithChangeAsync(change,
                        ethereumTxRequest.SignedTransfer.Sign, ethereumTxRequest.SignedTransfer.Id,
                        asset, fromAddress, _settings.HotwalletAddress, ethereumTxRequest.Volume);
                }
                else
                {
                    res = await _srvEthereumHelper.SendTransferAsync(ethereumTxRequest.SignedTransfer.Id, ethereumTxRequest.SignedTransfer.Sign,
                        asset, fromAddress, _settings.HotwalletAddress, ethereumTxRequest.Volume);
                }

                if (res.HasError)
                {
                    errMsg = res.Error.ToJson();
                    await _log.WriteWarningAsync(nameof(TradeQueue), nameof(ProcessEthGuaranteeTransfer), errMsg, string.Empty);
                }

                ethereumTxRequest.OperationIds =
                    clientTrades.Where(x => x.ClientId == ethereumTxRequest.ClientId && x.Amount < 0)
                        .Select(x => x.Id)
                        .ToArray();
                await _ethereumTransactionRequestRepository.UpdateAsync(ethereumTxRequest);
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(TradeQueue), nameof(ProcessEthGuaranteeTransfer), e.Message, e);

                errMsg = $"{e.GetType()}\n{e.Message}";
            }

            if (!string.IsNullOrEmpty(errMsg))
            {
                await _ethClientEventLogs.WriteEvent(ethereumTxRequest.ClientId, Event.Error, new
                {
                    Info = $"Guarantee transfer of {asset.Id} failed",
                    Operations = operations.ToJson(),
                    RequestId = ethereumTxRequest.Id,
                    Error = errMsg
                }.ToJson());
                return false;
            }

            return true;
        }

        private async Task CreateTransaction(string orderId, List<AggregatedTransfer> operations, IClientTrade[] trades)
        {
            var contextData = await _bitcoinTransactionService.GetTransactionContext<SwapOffchainContextData>(orderId) ?? new SwapOffchainContextData();

            foreach (var operation in operations)
            {
                var trade = trades.FirstOrDefault(x => x.ClientId == operation.ClientId && x.AssetId == operation.AssetId && Math.Abs(x.Amount - (double)operation.Amount) < 0.00000001);

                // find existed operation (which was inserted in LW after guarantee transfer)
                var existed = contextData.Operations.FirstOrDefault(x => x.ClientId == operation.ClientId && x.AssetId == operation.AssetId);

                if (existed != null)
                {
                    existed.ClientTradeId = trade?.Id;
                    continue;
                }

                contextData.Operations.Add(new SwapOffchainContextData.Operation()
                {
                    TransactionId = operation.TransferId,
                    Amount = operation.Amount,
                    ClientId = operation.ClientId,
                    AssetId = operation.AssetId,
                    ClientTradeId = trade?.Id
                });
            }

            await _bitcoinTransactionsRepository.CreateAsync(orderId, BitCoinCommands.SwapOffchain, "", null, "");
            await _bitcoinTransactionService.SetTransactionContext(orderId, contextData);
        }

        private List<AggregatedTransfer> AggregateSwaps(IEnumerable<TradeQueueItem.TradeInfo> swaps)
        {
            var list = new List<AggregatedTransfer>();

            foreach (var swap in swaps)
            {
                var amount1 = Convert.ToDecimal(swap.MarketVolume);
                var amount2 = Convert.ToDecimal(swap.LimitVolume);

                AddAmount(list, swap.MarketClientId, swap.MarketAsset, -amount1);
                AddAmount(list, swap.LimitClientId, swap.MarketAsset, amount1);

                AddAmount(list, swap.LimitClientId, swap.LimitAsset, -amount2);
                AddAmount(list, swap.MarketClientId, swap.LimitAsset, amount2);
            }

            return list;
        }

        private void AddAmount(ICollection<AggregatedTransfer> list, string client, string asset, decimal amount)
        {
            var client1 = list.FirstOrDefault(x => x.ClientId == client && x.AssetId == asset);
            if (client1 != null)
                client1.Amount += amount;
            else
                list.Add(new AggregatedTransfer
                {
                    Amount = amount,
                    ClientId = client,
                    AssetId = asset,
                    TransferId = Guid.NewGuid().ToString()
                });
        }

        private class AggregatedTransfer
        {
            public string ClientId { get; set; }

            public string AssetId { get; set; }

            public decimal Amount { get; set; }

            public string TransferId { get; set; }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}