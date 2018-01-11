﻿using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Services.Messages.Email;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.ClientAccount.Client;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class NotificationsProjection
    {
        private readonly ILog _log;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ISrvEmailsFacade _srvEmailsFacade;

        public NotificationsProjection(
            [NotNull] ILog log,
            [NotNull] IClientAccountClient clientAccountClient,
            [NotNull] ISrvEmailsFacade srvEmailsFacade)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clientAccountClient = clientAccountClient ?? throw new ArgumentNullException(nameof(clientAccountClient));
            _srvEmailsFacade = srvEmailsFacade ?? throw new ArgumentNullException(nameof(srvEmailsFacade));
        }

        public async Task Handle(SolarCashOutCompletedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(NotificationsProjection), nameof(SolarCashOutCompletedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var slrAddress = new SolarCoinAddress(evt.Address);
            var clientAcc = await _clientAccountClient.GetByIdAsync(evt.ClientId);

            await _srvEmailsFacade.SendSolarCashOutCompletedEmail(clientAcc.PartnerId, clientAcc.Email, slrAddress.Value, evt.Amount);
        }

    }
}
