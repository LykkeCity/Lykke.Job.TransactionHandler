using System;
using Autofac;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Services;
using Lykke.MatchingEngine.Connector.Services;
using Lykke.SettingsReader;

namespace Lykke.Job.TransactionHandler.Modules
{
    public class MatchingEngineModule : Module
    {
        private readonly IReloadingManager<AppSettings.MatchingEngineSettings> _settings;
        private readonly ILog _log;

        public MatchingEngineModule(
            [NotNull] IReloadingManager<AppSettings.MatchingEngineSettings> settings, ILog log)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _log = log;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var socketLog = new SocketLogDynamic(i => { },
                str => _log.WriteInfo("MeClient", null, str));
            
            builder.BindMeClient(_settings.CurrentValue.IpEndpoint.GetClientIpEndPoint(), socketLog);
        }
    }
}
