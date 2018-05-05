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
            if (evt.IsTrustedClient)
                return;

            ChaosKitty.Meow();

            var cmd = new UpdateLimitOrdersCountCommand
            {
                ClientId = evt.LimitOrder.Order.ClientId,
                IsTrustedClient = evt.IsTrustedClient
            };

            commandSender.SendCommand(cmd, "operations-history");
        }        
    }
}
