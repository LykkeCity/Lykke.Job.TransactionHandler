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

        public static Task TriggerHostTask;

        public StartupManager(
            TriggerHost triggerHost,
            ICqrsEngine cqrsEngine)
        {
            _triggerHost = triggerHost;
            _cqrsEngine = cqrsEngine;
        }
        public Task StartAsync()
        {
            _cqrsEngine.StartSubscribers();
            _cqrsEngine.StartProcesses();

            TriggerHostTask = _triggerHost.Start();

            return Task.CompletedTask;
        }
    }
}
