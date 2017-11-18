using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Exchange
{
    public class MarketOrderEntity : TableEntity, IMarketOrder
    {

        public static class ByOrderId
        {
            public static string GeneratePartitionKey()
            {
                return "OrderId";
            }

            public static string GenerateRowKey(string orderId)
            {
                return orderId;
            }

            public static MarketOrderEntity Create(IMarketOrder marketOrder)
            {
                var entity = CreateNew(marketOrder);
                entity.RowKey = GenerateRowKey(marketOrder.Id);
                entity.PartitionKey = GeneratePartitionKey();
                return entity;
            }
        }

        public static class ByClientId
        {
            public static string GeneratePartitionKey(string clientId)
            {
                return clientId;
            }

            public static string GenerateRowKey(string orderId)
            {
                return orderId;
            }

            public static MarketOrderEntity Create(IMarketOrder marketOrder)
            {
                var entity = CreateNew(marketOrder);
                entity.RowKey = GenerateRowKey(marketOrder.Id);
                entity.PartitionKey = GeneratePartitionKey(marketOrder.ClientId);
                return entity;
            }
        }

        public static MarketOrderEntity CreateNew(IMarketOrder marketOrder)
        {
            return new MarketOrderEntity
            {
                AssetPairId = marketOrder.AssetPairId,
                ClientId = marketOrder.ClientId,
                CreatedAt = marketOrder.CreatedAt,
                Id = marketOrder.Id,
                MatchedAt = marketOrder.MatchedAt,
                Price = marketOrder.Price,
                Status = marketOrder.Status,
                Straight = marketOrder.Straight,
                Volume = marketOrder.Volume,
                MatchingId = marketOrder.MatchingId
            };
        }

        public DateTime CreatedAt { get; set; }
        public DateTime? MatchedAt { get; set; }
        public string MatchingId { get; set; }

        public double Price { get; set; }
        public string AssetPairId { get; set; }

        public double Volume { get; set; }

        public string Status { get; set; }
        public bool Straight { get; set; }
        public string Id { get; set; }
        public string ClientId { get; set; }
    }

    public class MarketOrdersRepository : IMarketOrdersRepository
    {
        private readonly INoSQLTableStorage<MarketOrderEntity> _tableStorage;

        public MarketOrdersRepository(INoSQLTableStorage<MarketOrderEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task<IMarketOrder> GetAsync(string orderId)
        {
            var partitionKey = MarketOrderEntity.ByOrderId.GeneratePartitionKey();
            var rowKey = MarketOrderEntity.ByOrderId.GenerateRowKey(orderId);

            return await _tableStorage.GetDataAsync(partitionKey, rowKey);
        }

        public async Task<IMarketOrder> GetAsync(string clientId, string orderId)
        {
            var partitionKey = MarketOrderEntity.ByClientId.GeneratePartitionKey(clientId);
            var rowKey = MarketOrderEntity.ByClientId.GenerateRowKey(orderId);

            return await _tableStorage.GetDataAsync(partitionKey, rowKey);
        }

        public async Task<IEnumerable<IMarketOrder>> GetOrdersAsync(string clientId)
        {
            var partitionKey = MarketOrderEntity.ByClientId.GeneratePartitionKey(clientId);

            return await _tableStorage.GetDataAsync(partitionKey);
        }

        public async Task<IEnumerable<IMarketOrder>> GetOrdersAsync(IEnumerable<string> orderIds)
        {
            var partitionKey = MarketOrderEntity.ByOrderId.GeneratePartitionKey();
            orderIds = orderIds.Select(MarketOrderEntity.ByOrderId.GenerateRowKey);

            return await _tableStorage.GetDataAsync(partitionKey, orderIds);
        }

        public async Task CreateAsync(IMarketOrder marketOrder)
        {
            var byOrderEntity = MarketOrderEntity.ByOrderId.Create(marketOrder);
            var byClientEntity = MarketOrderEntity.ByClientId.Create(marketOrder);

            await _tableStorage.InsertAsync(byOrderEntity);
            await _tableStorage.InsertAsync(byClientEntity);
        }
    }
}