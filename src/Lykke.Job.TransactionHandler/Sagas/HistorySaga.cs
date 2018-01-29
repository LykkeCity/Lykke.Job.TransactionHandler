using System.Threading.Tasks;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class HistorySaga
    {
        public async Task Handle(LimitOrderExecutedEvent evt, ICommandSender commandSender)
        {
            ChaosKitty.Meow();

            var cmd = new CreateOrUpdateLimitOrderCommand
            {
                LimitOrder = evt.LimitOrder.Order,
                IsTrustedClient = evt.IsTrustedClient
            };

            commandSender.SendCommand(cmd, "operations-history");
        }        
    }
}