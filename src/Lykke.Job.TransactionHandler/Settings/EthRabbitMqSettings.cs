using JetBrains.Annotations;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class EthRabbitMqSettings
    {
        public string ConnectionString { get; set; }
        public string ExchangeEthereumEvents { get; set; }
    }
}
