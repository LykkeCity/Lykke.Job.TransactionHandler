using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
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

        public HistoryCommandHandler(ILog log, ILimitOrdersRepository limitOrdersRepository, IClientCacheRepository clientCacheRepository)
        {
            _log = log;
            _limitOrdersRepository = limitOrdersRepository;
            _clientCacheRepository = clientCacheRepository;
        }

        [UsedImplicitly]
        public async Task<CommandHandlingResult> Handle(UpdateLimitOrdersCountCommand command, IEventPublisher eventPublisher)
        {
            var activeLimitOrdersCount = await _limitOrdersRepository.GetActiveOrdersCountAsync(command.ClientId);
            await _clientCacheRepository.UpdateLimitOrdersCount(command.ClientId, activeLimitOrdersCount);

            ChaosKitty.Meow();

            _log.WriteInfo(nameof(UpdateLimitOrdersCountCommand), null, $"Client {command.ClientId}. Limit orders cache updated: {activeLimitOrdersCount} active orders");

            return CommandHandlingResult.Ok();
        }
    }
}