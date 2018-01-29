using System.Text;
using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Services.Http;

namespace Lykke.Job.TransactionHandler.Services.Notifications
{
    public class SrvSlackNotifications
    {
        private readonly AppSettings.SlackIntegrationSettings _settings;

        public SrvSlackNotifications(AppSettings.SlackIntegrationSettings settings)
        {
            _settings = settings;
        }

        public async Task SendNotification(string type, string message, string sender = null)
        {
            var webHookUrl = _settings.GetChannelWebHook(type);
            if (webHookUrl != null)
            {
                var text = new StringBuilder();

                if (!string.IsNullOrEmpty(_settings.Env))
                    text.AppendLine($"Environment: {_settings.Env}");

                text.AppendLine(sender != null ? $"{sender} : {message}" : message);

                await
                    new HttpRequestClient().PostRequest(new { text = text.ToString() }.ToJson(),
                        webHookUrl);
            }
        }
    }
}