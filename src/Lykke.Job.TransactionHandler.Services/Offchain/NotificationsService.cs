using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Services.AppNotifications;
using Lykke.Service.ClientAccount.Client;

namespace Lykke.Job.TransactionHandler.Services.Offchain
{
    public class NotificationsService : INotificationsService
    {
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IAppNotifications _appNotifications;

        public NotificationsService(IClientSettingsRepository clientSettingsRepository, IClientAccountClient clientAccountClient, IAppNotifications appNotifications)
        {
            _clientSettingsRepository = clientSettingsRepository;
            _clientAccountClient = clientAccountClient;
            _appNotifications = appNotifications;
        }

        public async Task OffchainNotifyUser(string clientId)
        {
            var pushSettings = await _clientSettingsRepository.GetSettings<PushNotificationsSettings>(clientId);
            if (pushSettings.Enabled)
            {
                var clientAcc = await _clientAccountClient.GetByIdAsync(clientId);

                await _appNotifications.SendDataNotificationToAllDevicesAsync(new[] { clientAcc.NotificationsId }, NotificationType.OffchainRequest, "Wallet");
            }
        }
    }
}