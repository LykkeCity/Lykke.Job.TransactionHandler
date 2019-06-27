using JetBrains.Annotations;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class MongoDeduplicatorSettings
    {
        public string ConnectionString { get; set; }
        public string CollectionName { get; set; }
    }
}
