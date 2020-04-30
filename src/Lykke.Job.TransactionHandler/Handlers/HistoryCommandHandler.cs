using System.Diagnostics;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class HistoryCommandHandler
    {
        private readonly ILog _log;
        private readonly ILimitOrdersRepository _limitOrdersRepository;
        private readonly IClientCacheRepository _clientCacheRepository;

        public HistoryCommandHandler(ILogFactory logFactory, ILimitOrdersRepository limitOrdersRepository, IClientCacheRepository clientCacheRepository)
        {
            _log = logFactory.CreateLog(this);
            _limitOrdersRepository = limitOrdersRepository;
            _clientCacheRepository = clientCacheRepository;
        }

        [UsedImplicitly]
        public async Task<CommandHandlingResult> Handle(UpdateLimitOrdersCountCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                var activeLimitOrdersCount = await _limitOrdersRepository.GetActiveOrdersCountAsync(command.ClientId);
                await _clientCacheRepository.UpdateLimitOrdersCount(command.ClientId, activeLimitOrdersCount);

                ChaosKitty.Meow();

                _log.Info(nameof(UpdateLimitOrdersCountCommand), $"Client {command.ClientId}. Limit orders cache updated: {activeLimitOrdersCount} active orders");

                return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(HistoryCommandHandler),  Command = nameof(UpdateLimitOrdersCountCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }
    }
}
