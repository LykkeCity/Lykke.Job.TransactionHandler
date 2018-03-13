using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Chaos;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Commands.EthereumCore;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Events.EthereumCore;
using Lykke.Service.Assets.Client;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class EthereumCoreSaga
    {
        private readonly ILog _log;
        private readonly IEthereumCashinAggregateRepository _ethereumCashinAggregateRepository;

        public EthereumCoreSaga(
            [NotNull] ILog log,
            [NotNull] IEthereumCashinAggregateRepository ethereumCashinAggregateRepository)
        {
            _log = log?.CreateComponentScope(nameof(EthereumCoreSaga)) ?? throw new ArgumentNullException(nameof(log));
            _ethereumCashinAggregateRepository = ethereumCashinAggregateRepository;
        }

        private async Task Handle(CashinDetectedEvent evt, ICommandSender sender)
        {
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();

                _log.WriteInfo(evt.TransactionHash, evt, "Eth Cashin startetd");

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
                _log.WriteError(evt.TransactionHash, evt, e);
                throw;
            }
            finally
            {
                _log.WriteInfo(evt.TransactionHash, evt, $"Eth Cashin start completed in {sw.ElapsedMilliseconds}");
                sw.Stop();
            }
        }

        private async Task Handle(EthCashinEnrolledToMatchingEngineEvent evt, ICommandSender sender)
        {
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();
                _log.WriteInfo(evt.TransactionHash, evt, "Cashin Enrolled To ME");

                var aggregate = await _ethereumCashinAggregateRepository.TryGetAsync(evt.TransactionHash);

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
                _log.WriteError(evt.TransactionHash, evt, e);
                throw;
            }
            finally
            {
                _log.WriteInfo(evt.TransactionHash, evt, $"Cashin Enrolled To ME in {sw.ElapsedMilliseconds}");
                sw.Stop();
            }
        }

        private async Task Handle(EthCashinSavedInHistoryEvent evt, ICommandSender sender)
        {
            Stopwatch sw = new Stopwatch();
            try
            {
                sw.Start();
                _log.WriteInfo(evt.TransactionHash, evt, "Cashin save history start");

                var aggregate = await _ethereumCashinAggregateRepository.TryGetAsync(evt.TransactionHash);

                if (aggregate.OnHistorySavedEvent())
                {
                    await _ethereumCashinAggregateRepository.SaveAsync(aggregate);
                }
            }
            catch (Exception e)
            {
                _log.WriteError(evt.TransactionHash, evt, e);
                throw;
            }
            finally
            {
                _log.WriteInfo(evt.TransactionHash, evt, $"Cashin save history completed in {sw.ElapsedMilliseconds}");
                sw.Stop();
            }
        }
    }
}