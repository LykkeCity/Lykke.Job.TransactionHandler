using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Bitcoin.Api.Client.BitcoinApi;
using Lykke.Bitcoin.Api.Client.BitcoinApi.Models;
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
            [NotNull] ILog log,
            [NotNull] IBitcoinApiClient bitcoinApiClient,
            TimeSpan retryTimeout)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _bitcoinApiClient = bitcoinApiClient ?? throw new ArgumentNullException(nameof(bitcoinApiClient));
            _retryTimeout = retryTimeout;
        }

        public async Task<CommandHandlingResult> Handle(Commands.BitcoinCashOutCommand command)
        {
            await _log.WriteInfoAsync(nameof(BitcoinCommandHandler), nameof(Commands.BitcoinCashOutCommand), command.ToJson(), "");

            var response = await _bitcoinApiClient.CashoutAsync(new CashoutModel
            {
                Amount = (decimal)command.Amount,
                AssetId = command.AssetId,
                DestinationAddress = command.Address,
                TransactionId = Guid.Parse(command.TransactionId)
            });
            if (response.HasError && response.Error.ErrorCode != ErrorCode.DuplicateTransactionId)
            {
                await _log.WriteErrorAsync(nameof(BitcoinCommandHandler), nameof(Commands.BitcoinCashOutCommand), command.ToJson(), new Exception(response.ToJson()));
                return CommandHandlingResult.Fail(_retryTimeout);
            }

            ChaosKitty.Meow();

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(Commands.SegwitTransferCommand command)
        {
            await _log.WriteInfoAsync(nameof(BitcoinCommandHandler), nameof(Commands.SegwitTransferCommand), command.ToJson(), "");

            var response = await _bitcoinApiClient.SegwitTransfer(Guid.Parse(command.Id), command.Address);
            if (response.HasError && response.Error.ErrorCode != ErrorCode.DuplicateTransactionId)
            {
                await _log.WriteErrorAsync(nameof(BitcoinCommandHandler), nameof(Commands.SegwitTransferCommand), command.ToJson(), new Exception(response.ToJson()));
                return CommandHandlingResult.Fail(_retryTimeout);
            }

            ChaosKitty.Meow();

            return CommandHandlingResult.Ok();
        }
    }
}