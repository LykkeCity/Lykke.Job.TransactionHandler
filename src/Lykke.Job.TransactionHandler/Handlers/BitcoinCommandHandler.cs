using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Bitcoin.Api.Client.BitcoinApi;
using Lykke.Bitcoin.Api.Client.BitcoinApi.Models;
using Lykke.Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class BitcoinCommandHandler
    {
        private readonly ILog _log;
        private readonly IBitcoinApiClient _bitcoinApiClient;
        private readonly TimeSpan _retryTimeout;

        public BitcoinCommandHandler(
            [NotNull] ILogFactory logFactory,
            [NotNull] IBitcoinApiClient bitcoinApiClient,
            TimeSpan retryTimeout)
        {
            _log = logFactory.CreateLog(this) ?? throw new ArgumentNullException(nameof(logFactory));
            _bitcoinApiClient = bitcoinApiClient ?? throw new ArgumentNullException(nameof(bitcoinApiClient));
            _retryTimeout = retryTimeout;
        }

        public async Task<CommandHandlingResult> Handle(Commands.SegwitTransferCommand command)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                var response = await _bitcoinApiClient.SegwitTransfer(Guid.Parse(command.Id), command.Address);
                if (response.HasError && response.Error.ErrorCode != ErrorCode.DuplicateTransactionId)
                {
                    _log.Error($"{nameof(BitcoinCommandHandler)}:{nameof(Commands.SegwitTransferCommand)}", new Exception(response.ToJson()), context: command.ToJson());
                    return CommandHandlingResult.Fail(_retryTimeout);
                }

                ChaosKitty.Meow();

                return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { TxHandler = new { Handler = nameof(BitcoinCommandHandler),  Command = nameof(Commands.SegwitTransferCommand),
                        Time = sw.ElapsedMilliseconds
                    }});
            }
        }
    }
}
