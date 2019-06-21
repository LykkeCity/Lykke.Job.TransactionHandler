using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands.EthereumCore;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Events.EthereumCore;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class EthereumCoreSaga
    {
        private readonly ILog _log;
        private readonly IEthereumCashinAggregateRepository _ethereumCashinAggregateRepository;

        public EthereumCoreSaga(
            [NotNull] ILogFactory logFactory,
            [NotNull] IEthereumCashinAggregateRepository ethereumCashinAggregateRepository)
        {
            _log = logFactory.CreateLog(this) ?? throw new ArgumentNullException(nameof(logFactory));
            _ethereumCashinAggregateRepository = ethereumCashinAggregateRepository;
        }

        public async Task Handle(CashinDetectedEvent evt, ICommandSender sender)
        {
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();

                _log.Info(evt.TransactionHash, "Eth Cashin startetd", evt);

                var aggregate = await _ethereumCashinAggregateRepository.GetOrAddAsync(evt.TransactionHash, () =>
                                    new EthereumCashinAggregate(evt.TransactionHash, evt.ClientId, evt.AssetId,
                                    evt.ClientAddress, evt.Amount, evt.CreatePendingActions));

                if (aggregate.State == EthereumCashinState.CashinStarted)
                {
                    sender.SendCommand(new EnrollEthCashinToMatchingEngineCommand()
                    {
                        TransactionHash = evt.TransactionHash,
                        Amount = evt.Amount,
                        AssetId = evt.AssetId,
                        ClientAddress = evt.ClientAddress,
                        ClientId = evt.ClientId,
                        CreatePendingActions = evt.CreatePendingActions,
                        CashinOperationId = aggregate.CashinOperationId
                    }, BoundedContexts.EthereumCommands);
                }
            }
            catch (Exception e)
            {
                _log.Error(nameof(CashinDetectedEvent), e, context: evt);
                throw;
            }
            finally
            {
                _log.Info(nameof(CashinDetectedEvent), $"Eth Cashin start completed in {sw.ElapsedMilliseconds}", evt);
                sw.Stop();
            }
        }

        public async Task Handle(EthCashinEnrolledToMatchingEngineEvent evt, ICommandSender sender)
        {
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();
                _log.Info(evt.TransactionHash, "Cashin Enrolled To ME", evt);

                var aggregate = await _ethereumCashinAggregateRepository.GetAsync(evt.TransactionHash);

                if (aggregate.OnEnrolledToMatchingEngineEvent())
                {
                    sender.SendCommand(new SaveEthInHistoryCommand()
                    {
                        TransactionHash = aggregate.TransactionHash,
                        Amount = aggregate.Amount,
                        AssetId = aggregate.AssetId,
                        ClientAddress = aggregate.ClientAddress,
                        ClientId = aggregate.ClientId,
                        CashinOperationId = aggregate.CashinOperationId
                    }, BoundedContexts.EthereumCommands);

                    await _ethereumCashinAggregateRepository.SaveAsync(aggregate);
                }
            }
            catch (Exception e)
            {
                _log.Error(nameof(EthCashinEnrolledToMatchingEngineEvent), e, context: evt);
                throw;
            }
            finally
            {
                _log.Info(nameof(EthCashinEnrolledToMatchingEngineEvent), $"Cashin Enrolled To ME in {sw.ElapsedMilliseconds}", evt);
                sw.Stop();
            }
        }

        public async Task Handle(EthCashinSavedInHistoryEvent evt, ICommandSender sender)
        {
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();
                _log.Info(evt.TransactionHash, "Cashin save history start", evt);

                var aggregate = await _ethereumCashinAggregateRepository.GetAsync(evt.TransactionHash);

                if (aggregate.OnHistorySavedEvent())
                {
                    await _ethereumCashinAggregateRepository.SaveAsync(aggregate);
                }
            }
            catch (Exception e)
            {
                _log.Error(nameof(EthCashinSavedInHistoryEvent), e, context: evt);
                throw;
            }
            finally
            {
                _log.Info(nameof(EthCashinSavedInHistoryEvent), $"Cashin save history completed in {sw.ElapsedMilliseconds}", evt);
                sw.Stop();
            }
        }
    }
}
