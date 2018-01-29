using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class ProcessEthereumCashoutCommand : ProcessCashOutBaseCommand
    {
        public string ClientId { get; set; }

        public string AssetId { get; set; }

        public string CashOperationId { get; set; }
    }
}
