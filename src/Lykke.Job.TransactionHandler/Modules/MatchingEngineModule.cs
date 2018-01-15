using System;
using Autofac;
using Common;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Services;
using Lykke.MatchingEngine.Connector.Services;
using Lykke.SettingsReader;

namespace Lykke.Job.TransactionHandler.Modules
{
    public class MatchingEngineModule : Module
    {
        private readonly IReloadingManager<AppSettings.MatchingEngineSettings> _settings;

        public MatchingEngineModule(
            [NotNull] IReloadingManager<AppSettings.MatchingEngineSettings> settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        protected override void Load(ContainerBuilder builder)
        {
            var socketLog = new SocketLogDynamic(i => { },
                str => Console.WriteLine(DateTime.UtcNow.ToIsoDateTime() + ": " + str));
            
            builder.BindMeClient(_settings.CurrentValue.IpEndpoint.GetClientIpEndPoint(), socketLog);
        }
    }
}
