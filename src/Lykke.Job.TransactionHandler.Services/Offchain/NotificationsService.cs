using System.Threading.Tasks;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.PushNotifications.Contract;
using Lykke.Service.PushNotifications.Contract.Commands;
using Lykke.Service.PushNotifications.Contract.Enums;

namespace Lykke.Job.TransactionHandler.Services.Offchain
{
    public class NotificationsService : INotificationsService
    {
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ICqrsEngine _cqrsEngine;

        public NotificationsService(
            IClientSettingsRepository clientSettingsRepository, 
            IClientAccountClient clientAccountClient, 
            ICqrsEngine cqrsEngine
            )
        {
            _clientSettingsRepository = clientSettingsRepository;
            _clientAccountClient = clientAccountClient;
            _cqrsEngine = cqrsEngine;
        }

        public async Task OffchainNotifyUser(string clientId)
        {
            var pushSettings = await _clientSettingsRepository.GetSettings<PushNotificationsSettings>(clientId);
            
            if (pushSettings.Enabled)
            {
                var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);

                if (string.IsNullOrEmpty(clientAcc.NotificationsId))
                    return;
                
                _cqrsEngine.SendCommand(new DataNotificationCommand
                {
                    NotificationIds = new[] { clientAcc.NotificationsId },
                    Type = NotificationType.OffchainRequest.ToString(),
                    Entity = "Wallet"
                }, "tx-handler.offchain", PushNotificationsBoundedContext.Name);
            }
        }
    }
}
