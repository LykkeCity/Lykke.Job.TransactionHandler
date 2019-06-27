using JetBrains.Annotations;
using Lykke.Sdk.Settings;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.OperationsRepository.Client;
using Lykke.Service.PersonalData.Settings;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class AppSettings : BaseAppSettings
    {
        public TransactionHandlerSettings TransactionHandlerJob { get; set; }
        public AssetsSettings Assets { get; set; }
        public ClientAccountServiceClientSettings ClientAccountClient { get; set; }
        public EthereumSettings Ethereum { get; set; }
        public BitcoinCoreSettings BitCoinCore { get; set; }
        public MatchingEngineSettings MatchingEngineClient { get; set; }
        public RabbitMqSettings RabbitMq { get; set; }
        public EthRabbitMqSettings EthRabbitMq { get; set; }
        public PersonalDataServiceClientSettings PersonalDataServiceSettings { get; set; }
        public OperationsRepositoryServiceClientSettings OperationsRepositoryServiceClient { get; set; }
    }
}
