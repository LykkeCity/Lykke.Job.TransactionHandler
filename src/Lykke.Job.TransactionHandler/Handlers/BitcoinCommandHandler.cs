using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Bitcoin.Api.Client.BitcoinApi;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Utils;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class BitcoinCommandHandler
    {
        private readonly ILog _log;
        private readonly IBitcoinCommandSender _bitcoinCommandSender;
        private readonly IBitcoinApiClient _bitcoinApiClient;
        private readonly TimeSpan _retryTimeout;

        public BitcoinCommandHandler(
            [NotNull] ILog log,
            [NotNull] IBitcoinCommandSender bitcoinCommandSender,
            [NotNull] IBitcoinApiClient bitcoinApiClient,
            TimeSpan retryTimeout)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _bitcoinCommandSender = bitcoinCommandSender ?? throw new ArgumentNullException(nameof(bitcoinCommandSender));
            _bitcoinApiClient = bitcoinApiClient ?? throw new ArgumentNullException(nameof(bitcoinApiClient));
            _retryTimeout = retryTimeout;
        }

        public async Task<CommandHandlingResult> Handle(Commands.SendBitcoinCommand command)
        {
            await _log.WriteInfoAsync(nameof(BitcoinCommandHandler), nameof(Commands.SendBitcoinCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            await _bitcoinCommandSender.SendCommand(command.Command);

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(Commands.BitcoinCashOutCommand command)
        {
            await _log.WriteInfoAsync(nameof(BitcoinCommandHandler), nameof(Commands.BitcoinCashOutCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            var response = await _bitcoinApiClient.CashoutAsync(new Bitcoin.Api.Client.BitcoinApi.Models.CashoutModel
            {
                Amount = (decimal)command.Amount,
                AssetId = command.AssetId,
                DestinationAddress = command.Address,
                TransactionId = Guid.Parse(command.TransactionId)
            });
            if (response.HasError)
            {
                await _log.WriteErrorAsync(nameof(BitcoinCommandHandler), nameof(Commands.BitcoinCashOutCommand), command.ToJson(), new Exception(response.ToJson()));
                return CommandHandlingResult.Fail(_retryTimeout);
            }

            return CommandHandlingResult.Ok();
        }
    }
}