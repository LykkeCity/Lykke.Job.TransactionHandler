using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class CreateOffchainCashoutRequestCommand
    {
        public string Id { get; set; }

        public string ClientId { get; set; }

        public string AssetId { get; set; }

        public decimal Amount { get; set; }
    }
}