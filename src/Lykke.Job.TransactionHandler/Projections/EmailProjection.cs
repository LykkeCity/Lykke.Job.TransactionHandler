using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.PersonalData.Contract;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class EmailProjection
    {
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;
        private readonly IPersonalDataService _personalDataService;

        public EmailProjection(
            [NotNull] IClientAccountClient clientAccountClient,
            [NotNull] ISrvEmailsFacade srvEmailsFacade,
            [NotNull] IPersonalDataService personalDataService)
        {
            _clientAccountClient = clientAccountClient ?? throw new ArgumentNullException(nameof(clientAccountClient));
            _srvEmailsFacade = srvEmailsFacade ?? throw new ArgumentNullException(nameof(srvEmailsFacade));
            _personalDataService = personalDataService ?? throw new ArgumentNullException(nameof(personalDataService));
        }

        public async Task Handle(SolarCashOutCompletedEvent evt)
        {
            var slrAddress = new SolarCoinAddress(evt.Address);
            var clientAcc = await _clientAccountClient.GetByIdAsync(evt.ClientId);
            var clientEmail = await _personalDataService.GetEmailAsync(evt.ClientId);

            await _srvEmailsFacade.SendSolarCashOutCompletedEmail(clientAcc.PartnerId, clientEmail, slrAddress.Value, evt.Amount);

            ChaosKitty.Meow();
        }

    }
}
