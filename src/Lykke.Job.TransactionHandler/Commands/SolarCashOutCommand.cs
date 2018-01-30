using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class SolarCashOutCommand : ProcessCashOutBaseCommand
    {
        public string ClientId { get; set; }
    }
}
