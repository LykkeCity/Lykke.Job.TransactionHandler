using System.Threading.Tasks;
using Lykke.Cqrs;
using Lykke.JobTriggers.Triggers;
using Lykke.Sdk;

namespace Lykke.Job.TransactionHandler.Services
{
    public class StartupManager : IStartupManager
    {
        private readonly TriggerHost _triggerHost;
        private readonly ICqrsEngine _cqrsEngine;

        public StartupManager(
            TriggerHost triggerHost,
            ICqrsEngine cqrsEngine)
        {
            _triggerHost = triggerHost;
            _cqrsEngine = cqrsEngine;
        }
        public async Task StartAsync()
        {
            await _triggerHost.Start();

            _cqrsEngine.StartSubscribers();
            _cqrsEngine.StartProcesses();
        }
    }
}
