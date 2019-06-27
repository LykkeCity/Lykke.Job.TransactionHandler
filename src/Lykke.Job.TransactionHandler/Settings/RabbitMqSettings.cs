using JetBrains.Annotations;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class RabbitMqSettings
    {
        public string ConnectionString { get; set; }
        public string NewMeRabbitConnString { get; set; }
        public string AlternateConnectionString { get; set; }
        public string EventsExchange { get; set; }

        public string ExchangeSwap { get; set; }
        public string ExchangeLimit { get; set; }
        public string ExchangeCashOperation { get; set; }
        public string ExchangeTransfer { get; set; }
    }
}
