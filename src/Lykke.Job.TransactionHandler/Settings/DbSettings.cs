using JetBrains.Annotations;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class DbSettings
    {
        [AzureTableCheck]
        public string LogsConnString { get; set; }
        [AzureTableCheck]
        public string BitCoinQueueConnectionString { get; set; }
        [AzureTableCheck]
        public string ClientPersonalInfoConnString { get; set; }
        [AzureTableCheck]
        public string HMarketOrdersConnString { get; set; }
        [AzureTableCheck]
        public string OffchainConnString { get; set; }
    }
}
