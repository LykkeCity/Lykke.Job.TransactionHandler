using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OffchainCommandHandler
    {
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly ILog _log;

        public OffchainCommandHandler(
            [NotNull] IOffchainRequestService offchainRequestService,
            ILogFactory logFactory
            )
        {
            _offchainRequestService = offchainRequestService ?? throw new ArgumentNullException(nameof(offchainRequestService));
            _log = logFactory.CreateLog(this);
        }

        public async Task<CommandHandlingResult> Handle(CreateOffchainCashoutRequestCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                await _offchainRequestService.CreateOffchainRequestAndNotify(
                    transactionId: command.Id,
                    clientId: command.ClientId,
                    assetId: command.AssetId,
                    amount: command.Amount,
                    orderId: null,
                    type: OffchainTransferType.TrustedCashout);

                ChaosKitty.Meow();

                return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(OffchainCommandHandler),  Command = nameof(CreateOffchainCashoutRequestCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }
    }
}
