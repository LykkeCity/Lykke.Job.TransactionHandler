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
