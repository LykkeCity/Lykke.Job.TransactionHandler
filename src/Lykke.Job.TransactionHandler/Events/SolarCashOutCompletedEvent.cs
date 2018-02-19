using MessagePack;

namespace Lykke.Job.TransactionHandler.Events
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SolarCashOutCompletedEvent
    {
        public decimal Amount { get; set; }

        public string Address { get; set; }

        public string ClientId { get; set; }
    }
}
