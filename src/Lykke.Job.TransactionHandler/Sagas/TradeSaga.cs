using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Events;

namespace Lykke.Job.TransactionHandler.Sagas
{

    public class TradeSaga
    {
        private readonly ILog _log;

        public TradeSaga(
            ILog log)
        {
            _log = log;
        }

        private async Task Handle(TradeCreatedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(TradeSaga), nameof(TradeCreatedEvent), evt.ToJson());

            if (evt.IsTrustedClient)
            {
                return;
            }

            var cmd = new CreateTransactionCommand
            {
                OrderId = evt.OrderId,
            };

            sender.SendCommand(cmd, "tx-handler");
        }
    }
}