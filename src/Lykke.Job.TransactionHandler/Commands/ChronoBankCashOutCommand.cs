using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class ChronoBankCashOutCommand : ProcessCashOutBaseCommand
    {
    }
}
