using System.Diagnostics;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Events;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class ForwardWithdrawalCommandHandler
    {
        private readonly ILog _log;

        public ForwardWithdrawalCommandHandler(
            ILogFactory logFactory
            )
        {
            _log = logFactory.CreateLog(this);
        }

        public async Task<CommandHandlingResult> Handle(Commands.SetLinkedCashInOperationCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                eventPublisher.PublishEvent(new ForwardWithdawalLinkedEvent { Message = command.Message });
                return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { TxHandler = new { Handler = nameof(ForwardWithdrawalCommandHandler),  Command = nameof(Commands.SetLinkedCashInOperationCommand),
                        Time = sw.ElapsedMilliseconds
                    }});
            }
        }
    }
}
