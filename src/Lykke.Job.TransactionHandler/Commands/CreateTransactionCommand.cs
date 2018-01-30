using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class CreateTransactionCommand
    {
        public string OrderId { get; set; }
    }
}