using System;
using System.Threading.Tasks;
using AutoMapper;
using AzureStorage;
using Common;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Ethereum
{
    public class EthereumTransactionReqEntity : BaseEntity, IEthereumTransactionRequest
    {
        public static class ById
        {
            public static string GeneratePartition()
            {
                return "ETR";
            }

            public static string GenerateRowKey(Guid id)
            {
                return id.ToString();
            }

            public static EthereumTransactionReqEntity Create(IEthereumTransactionRequest request)
            {
                var entity = Mapper.Map<EthereumTransactionReqEntity>(request);
                entity.PartitionKey = GeneratePartition();
                entity.RowKey = GenerateRowKey(request.Id);

                return entity;
            }
        }

        public static class ByOrderId
        {
            public static string GeneratePartition()
            {
                return "OrderId";
            }

            public static string GenerateRowKey(string orderId)
            {
                return orderId;
            }

            public static EthereumTransactionReqEntity Create(IEthereumTransactionRequest request)
            {
                var entity = Mapper.Map<EthereumTransactionReqEntity>(request);
                entity.PartitionKey = GeneratePartition();
                entity.RowKey = GenerateRowKey(request.OrderId);

                return entity;
            }
        }

        public Guid Id { get; set; }
        public string ClientId { get; set; }
        public string Hash { get; set; }
        public string AssetId { get; set; }
        public decimal Volume { get; set; }
        public string AddressTo { get; set; }
        public string OrderId { get; set; }

        public string SignedTransferVal { get; set; }
        public Transaction SignedTransfer
        {
            get { return SignedTransferVal?.DeserializeJson<Transaction>(); }
            set { SignedTransferVal = value.ToJson(); }
        }

        public OperationType OperationType { get; set; }

        public string OperationIdsVal { get; set; }
        public string[] OperationIds
        {
            get { return OperationIdsVal?.DeserializeJson<string[]>(); }
            set { OperationIdsVal = value.ToJson(); }
        }
    }

    public class EthereumTransactionRequestRepository : IEthereumTransactionRequestRepository
    {
        private readonly INoSQLTableStorage<EthereumTransactionReqEntity> _tableStorage;

        public EthereumTransactionRequestRepository(INoSQLTableStorage<EthereumTransactionReqEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task<IEthereumTransactionRequest> GetAsync(Guid id)
        {
            var entity = await _tableStorage.GetDataAsync(EthereumTransactionReqEntity.ById.GeneratePartition(),
                EthereumTransactionReqEntity.ById.GenerateRowKey(id));
            return entity;
        }

        public async Task UpdateAsync(IEthereumTransactionRequest request)
        {
            var byId = EthereumTransactionReqEntity.ById.Create(request);
            await _tableStorage.InsertOrReplaceAsync(byId);

            if (!string.IsNullOrEmpty(request.OrderId))
            {
                var byOrder = EthereumTransactionReqEntity.ByOrderId.Create(request);
                await _tableStorage.InsertOrReplaceAsync(byOrder);
            }
        }
    }

}