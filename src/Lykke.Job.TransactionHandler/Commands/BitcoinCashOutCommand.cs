using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class BitcoinCashOutCommand : ProcessCashOutBaseCommand
    {
        public string AssetId { get; set; }
    }
}
