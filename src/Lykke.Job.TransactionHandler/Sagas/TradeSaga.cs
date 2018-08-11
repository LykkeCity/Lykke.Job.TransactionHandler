using System.Threading.Tasks;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Events;

namespace Lykke.Job.TransactionHandler.Sagas
{

    public class TradeSaga
    {
        public async Task Handle(TradeCreatedEvent evt, ICommandSender sender)
        {
            if (evt.IsTrustedClient)
            {
                return;
            }

            sender.SendCommand(new CreateTransactionCommand
            {
                OrderId = evt.OrderId,
            }, BoundedContexts.Trades);
        }
    }
}
