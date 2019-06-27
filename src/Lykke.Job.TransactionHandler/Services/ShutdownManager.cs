using System.Threading.Tasks;
using Lykke.JobTriggers.Triggers;
using Lykke.Sdk;

namespace Lykke.Job.TransactionHandler.Services
{
    public class ShutdownManager : IShutdownManager
    {
        private readonly TriggerHost _triggerHost;

        public ShutdownManager(
            TriggerHost triggerHost)
        {
            _triggerHost = triggerHost;
        }

        public Task StopAsync()
        {
            _triggerHost.Cancel();

            return Task.CompletedTask;
        }
    }
}
