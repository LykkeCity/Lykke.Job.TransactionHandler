using JetBrains.Annotations;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class TransactionHandlerSettings
    {
        public DbSettings Db { get; set; }
        public AssetsCacheSettings AssetsCache { get; set; }
        public string ExchangeOperationsServiceUrl { get; set; }
        public ServiceSettings Services { get; set; }
        public string QueuePostfix { get; set; }
        public long RetryDelayInMilliseconds { get; set; }
        public string SagasRabbitMqConnStr { get; set; }
        [Optional]
        public ChaosSettings ChaosKitty { get; set; }
        public MongoDeduplicatorSettings MongoDeduplicator { get; set; }
    }
}
