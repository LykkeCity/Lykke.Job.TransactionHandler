using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public abstract class ProcessCashOutBaseCommand
    {
        public string TransactionId { get; set; }

        public double Amount { get; set; }

        public string Address { get; set; }
    }
}
