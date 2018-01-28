using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Core.Domain.Fee;
using Lykke.Job.TransactionHandler.Core.Services.AppNotifications;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Queues;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Service.Assets.Client;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.OperationsRepository.AutorestClient;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using MessagePack;
using ProtoBuf;
using OrderStatus = Lykke.Service.OperationsRepository.AutorestClient.Models.OrderStatus;

namespace Lykke.Job.TransactionHandler.Handlers
{
    [UsedImplicitly]
    public class LimitOrderCommandHandler
    {
        private readonly ILog _log;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ILimitOrdersRepository _limitOrdersRepository;        
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        readonly Dictionary<string, bool> _trusted = new Dictionary<string, bool>();

        public LimitOrderCommandHandler(
            ILog log, 
            IClientAccountClient clientAccountClient, 
            ILimitOrdersRepository limitOrdersRepository,                         
            IAssetsServiceWithCache assetsServiceWithCache)
        {
            _log = log;
            _clientAccountClient = clientAccountClient;
            _limitOrdersRepository = limitOrdersRepository;            
            _assetsServiceWithCache = assetsServiceWithCache;            
        }

        [UsedImplicitly]
        public async Task<CommandHandlingResult> Handle(ProcessLimitOrderCommand command, IEventPublisher eventPublisher)
        {
            _log.WriteInfo(nameof(LimitOrderCommandHandler), command.ToJson(), "ProcessLimitOrderCommand");

            var clientId = command.LimitOrder.Order.ClientId;
            
            if (!_trusted.ContainsKey(clientId))
                _trusted[clientId] = (await _clientAccountClient.IsTrustedAsync(clientId)).Value;

            var isTrustedClient = _trusted[clientId];

            var limitOrderExecutedEvent = new LimitOrderExecutedEvent
            {
                IsTrustedClient = isTrustedClient,
                LimitOrder = command.LimitOrder
            };

            if (!isTrustedClient)
            {
                // need previous order state for not trusted clients
                var prevOrderState = await _limitOrdersRepository.GetOrderAsync(command.LimitOrder.Order.Id);                

                limitOrderExecutedEvent.HasPrevOrderState = prevOrderState != null;
                limitOrderExecutedEvent.PrevRemainingVolume = prevOrderState?.RemainingVolume;

                limitOrderExecutedEvent.Aggregated = AggregateSwaps(limitOrderExecutedEvent.LimitOrder.Trades);
            }

            var status = (OrderStatus)Enum.Parse(typeof(OrderStatus), command.LimitOrder.Order.Status);

            // workaround: ME sends wrong status
            if (status == OrderStatus.Processing && command.LimitOrder.Trades.Count == 0)
                status = OrderStatus.InOrderBook;

            if (status == OrderStatus.Processing || status == OrderStatus.Matched)
            {
                limitOrderExecutedEvent.Trades = await CreateTrades(command.LimitOrder);                
            }

            eventPublisher.PublishEvent(limitOrderExecutedEvent);
           
            return CommandHandlingResult.Ok();
        }        

        private async Task<ClientTrade[]> CreateTrades(LimitQueueItem.LimitOrderWithTrades limitOrderWithTrades)
        {
            if (limitOrderWithTrades.Trades == null || limitOrderWithTrades.Trades.Count == 0)
                return new ClientTrade[0];
            
            var trades = limitOrderWithTrades.ToDomainOffchain(limitOrderWithTrades.Order.Id, limitOrderWithTrades.Trades[0].ClientId);

            foreach (var trade in trades)
            {
                var tradeAsset = await _assetsServiceWithCache.TryGetAssetAsync(trade.AssetId);

                // already settled guarantee transaction or trusted asset
                if (trade.Amount < 0 || tradeAsset.IsTrusted)
                    trade.State = TransactionStates.SettledOffchain;
                else
                    trade.State = TransactionStates.InProcessOffchain;
            }

            return trades;
        }        
        
        private List<AggregatedTransfer> AggregateSwaps(IEnumerable<LimitQueueItem.LimitTradeInfo> trades)
        {
            var list = new List<AggregatedTransfer>();

            if (trades != null)
            {
                foreach (var swap in trades)
                {
                    var amount1 = Convert.ToDecimal(swap.Volume);
                    var amount2 = Convert.ToDecimal(swap.OppositeVolume);

                    AddAmount(list, swap.ClientId, swap.Asset, -amount1);
                    AddAmount(list, swap.OppositeClientId, swap.Asset, amount1);

                    AddAmount(list, swap.OppositeClientId, swap.OppositeAsset, -amount2);
                    AddAmount(list, swap.ClientId, swap.OppositeAsset, amount2);
                }
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
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class AggregatedTransfer
    {        
        public string ClientId { get; set; }
        
        public string AssetId { get; set; }
        
        public decimal Amount { get; set; }
        
        public string TransferId { get; set; }
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class ClientTrade
    {        
        public string Id { get; set; }
        public string ClientId { get; set; }       
        public string AssetId { get; set; }        
        public double Amount { get; set; }        
        public DateTime DateTime { get; set; }        
        public double Price { get; set; }        
        public string LimitOrderId { get; set; }        
        public string OppositeLimitOrderId { get; set; }        
        public string TransactionId { get; set; }        
        public bool IsLimitOrderResult { get; set; }        
        public TransactionStates State { get; set; }
    }
}