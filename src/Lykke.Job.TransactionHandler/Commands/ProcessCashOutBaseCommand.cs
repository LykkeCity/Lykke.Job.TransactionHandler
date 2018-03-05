using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public abstract class ProcessCashOutBaseCommand
    {
        public string TransactionId { get; set; }

        public decimal Amount { get; set; }

        public string Address { get; set; }
    }
}
