using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class ExternalCashInCommand
    {
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }
    }
}
