using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Job.TransactionHandler.AzureRepositories.BitCoin
{
    public class BitcoinCashinEntity : TableEntity, IBitcoinCashin
    {
        public string Id => RowKey;
        public string ClientId { get; set; }
        public string Address { get; set; }
        public string TxHash { get; set; }
        public bool IsSegwit { get; set; }

        public static string GeneratePartitionKey()
        {
            return "BitcoinCashin";
        }

        public static BitcoinCashinEntity Create(string id, string clientId, string address, string hash, bool isSegwit)
        {
            return new BitcoinCashinEntity
            {
                Address = address,
                ClientId = clientId,
                IsSegwit = isSegwit,
                TxHash = hash,
                PartitionKey = GeneratePartitionKey(),
                RowKey = id
            };
        }
    }

    public class BitcoinCashinRepository : IBitcoinCashinRepository
    {
        private readonly INoSQLTableStorage<BitcoinCashinEntity> _tableStorage;

        public BitcoinCashinRepository(INoSQLTableStorage<BitcoinCashinEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task<IBitcoinCashin> GetAsync(string id)
        {
            return await _tableStorage.GetDataAsync(BitcoinCashinEntity.GeneratePartitionKey(), id);
        }
    }
}
