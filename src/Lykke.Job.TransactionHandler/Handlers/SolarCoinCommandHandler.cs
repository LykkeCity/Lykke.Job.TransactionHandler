using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.ClientAccount.Client;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class SolarCoinCommandHandler
    {
        private readonly ILog _log;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly ISrvSolarCoinHelper _srvSolarCoinHelper;

        public SolarCoinCommandHandler(
            [NotNull] ILog log,
            [NotNull] IClientAccountClient clientAccountClient,
            [NotNull] ISrvEmailsFacade srvEmailsFacade,
            [NotNull] ISrvSolarCoinHelper srvSolarCoinHelper)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clientAccountClient = clientAccountClient ?? throw new ArgumentNullException(nameof(clientAccountClient));
            _srvEmailsFacade = srvEmailsFacade ?? throw new ArgumentNullException(nameof(srvEmailsFacade));
            _srvSolarCoinHelper = srvSolarCoinHelper ?? throw new ArgumentNullException(nameof(srvSolarCoinHelper));
        }

        public async Task<CommandHandlingResult> Handle(Commands.SendSolarCashOutCompletedEmailCommand command)
        {
            await _log.WriteInfoAsync(nameof(SolarCoinCommandHandler), nameof(Commands.SendSolarCashOutCompletedEmailCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            var slrAddress = new SolarCoinAddress(command.Address);
            var clientAcc = await _clientAccountClient.GetByIdAsync(command.ClientId);

            await _srvEmailsFacade.SendSolarCashOutCompletedEmail(clientAcc.PartnerId, clientAcc.Email, slrAddress.Value, command.Amount);

            return CommandHandlingResult.Ok();
        }

        public async Task<CommandHandlingResult> Handle(Commands.SolarCashOutCommand command)
        {
            await _log.WriteInfoAsync(nameof(SolarCoinCommandHandler), nameof(Commands.SolarCashOutCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            var slrAddress = new SolarCoinAddress(command.Address);

            await _srvSolarCoinHelper.SendCashOutRequest(command.TransactionId, slrAddress, command.Amount);

            return CommandHandlingResult.Ok();
        }

    }
}