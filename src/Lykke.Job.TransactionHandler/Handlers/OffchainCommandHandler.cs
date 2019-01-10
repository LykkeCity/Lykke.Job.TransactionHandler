using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.PushNotifications.Contract.Enums;
using Lykke.Service.PushNotifications.Contract.Events;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OffchainCommandHandler
    {
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IClientAccountClient _clientAccountClient;

        public OffchainCommandHandler(
            [NotNull] IOffchainRequestService offchainRequestService,
            IClientSettingsRepository clientSettingsRepository,
            IClientAccountClient clientAccountClient)
        {
            _offchainRequestService = offchainRequestService ?? throw new ArgumentNullException(nameof(offchainRequestService));
            _clientSettingsRepository = clientSettingsRepository;
            _clientAccountClient = clientAccountClient;
        }

        public async Task<CommandHandlingResult> Handle(CreateOffchainCashoutRequestCommand command, IEventPublisher eventPublisher)
        {
            await _offchainRequestService.CreateOffchainRequestAndNotify(
                transactionId: command.Id,
                clientId: command.ClientId,
                assetId: command.AssetId,
                amount: command.Amount,
                orderId: null,
                type: OffchainTransferType.TrustedCashout);

            var pushSettings = await _clientSettingsRepository.GetSettings<PushNotificationsSettings>(command.ClientId);
            if (pushSettings.Enabled)
            {
                var clientAcc = await _clientAccountClient.GetByIdAsync(command.ClientId);

                if (!string.IsNullOrEmpty(clientAcc.NotificationsId))
                    eventPublisher.PublishEvent(
                        new DataNotificationEvent
                        {
                            NotificationIds = new[] { clientAcc.NotificationsId },
                            Type = NotificationType.OffchainRequest.ToString(),
                            Entity = "Wallet"
                        });
            }

            ChaosKitty.Meow();

            return CommandHandlingResult.Ok();
        }
    }
}
