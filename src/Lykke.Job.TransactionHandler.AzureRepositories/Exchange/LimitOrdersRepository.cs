using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Exchange
{
    public class LimitOrderEntity : BaseEntity, ILimitOrder
    {
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

            public static LimitOrderEntity Create(ILimitOrder limitOrder)
            {
                var entity = CreateNew(limitOrder);
                entity.RowKey = GenerateRowKey(limitOrder.Id);
                entity.PartitionKey = GeneratePartitionKey(limitOrder.ClientId);
                return entity;
            }
        }

        public static class ByClientIdActive
        {
            public static string GeneratePartitionKey(string clientId)
            {
                return "Active_" + clientId;
            }

            public static string GenerateRowKey(string orderId)
            {
                return orderId;
            }

            public static LimitOrderEntity Create(ILimitOrder limitOrder)
            {
                var entity = CreateNew(limitOrder);
                entity.RowKey = GenerateRowKey(limitOrder.Id);
                entity.PartitionKey = GeneratePartitionKey(limitOrder.ClientId);
                return entity;
            }
        }

        public static class ByDate
        {
            public static string GeneratePartitionKey(DateTime date)
            {
                return date.ToString("yyyy-MM-dd");
            }

            public static string GenerateRowKey(string orderId)
            {
                return orderId;
            }

            public static LimitOrderEntity Create(ILimitOrder limitOrder)
            {
                var entity = CreateNew(limitOrder);
                entity.RowKey = GenerateRowKey(limitOrder.Id);
                entity.PartitionKey = GeneratePartitionKey(limitOrder.CreatedAt);
                return entity;
            }
        }

        public static LimitOrderEntity CreateNew(ILimitOrder limitOrder)
        {
            return new LimitOrderEntity
            {
                AssetPairId = limitOrder.AssetPairId,
                ClientId = limitOrder.ClientId,
                CreatedAt = limitOrder.CreatedAt,
                Id = limitOrder.Id,
                Price = limitOrder.Price,
                Status = limitOrder.Status,
                Straight = limitOrder.Straight,
                Volume = limitOrder.Volume,
                RemainingVolume = limitOrder.RemainingVolume,
                MatchingId = limitOrder.MatchingId
            };
        }

        public DateTime CreatedAt { get; set; }

        public double Price { get; set; }
        public string AssetPairId { get; set; }

        public double Volume { get; set; }

        public string Status { get; set; }
        public bool Straight { get; set; }
        public string Id { get; set; }
        public string ClientId { get; set; }

        public double RemainingVolume { get; set; }
        public string MatchingId { get; set; }
    }

    public class LimitOrdersRepository : ILimitOrdersRepository
    {
        private readonly INoSQLTableStorage<LimitOrderEntity> _tableStorage;

        public LimitOrdersRepository(INoSQLTableStorage<LimitOrderEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task CreateOrUpdateAsync(ILimitOrder limitOrder)
        {
            var tasks = new List<Task>
            {
                _tableStorage.InsertOrMergeAsync(LimitOrderEntity.ByDate.Create(limitOrder)),
                _tableStorage.InsertOrMergeAsync(LimitOrderEntity.ByClientId.Create(limitOrder)),
            };

            var status = (OrderStatus)Enum.Parse(typeof(OrderStatus), limitOrder.Status);
            if (status == OrderStatus.InOrderBook || status == OrderStatus.Processing)
            {
                tasks.Add(_tableStorage.InsertOrMergeAsync(LimitOrderEntity.ByClientIdActive.Create(limitOrder)));
            }
            else
            {
                tasks.Add(_tableStorage.DeleteIfExistAsync(LimitOrderEntity.ByClientIdActive.GeneratePartitionKey(limitOrder.ClientId), limitOrder.Id));
            }

            await Task.WhenAll(tasks.ToArray());
        }

        public async Task<ILimitOrder> GetOrderAsync(string clientId, string orderId)
        {
            return await _tableStorage.GetDataAsync(clientId, orderId);
        }

        public async Task<IEnumerable<ILimitOrder>> GetActiveOrdersAsync(string clientId)
        {
            var partitionKey = LimitOrderEntity.ByClientIdActive.GeneratePartitionKey(clientId);

            return await _tableStorage.GetDataAsync(partitionKey);
        }
    }
}