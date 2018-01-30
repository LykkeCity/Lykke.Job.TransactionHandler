using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SegwitTransferCommand
    {
        public string Id { get; set; }

        public string Address { get; set; }
    }
}