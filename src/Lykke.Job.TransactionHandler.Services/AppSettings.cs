using Lykke.Service.OperationsRepository.Client;
using Lykke.Service.PersonalData.Settings;
using Lykke.SettingsReader.Attributes;
using System;
using System.Linq;
using System.Net;

namespace Lykke.Job.TransactionHandler.Services
{
    public class AppSettings
    {
        public TransactionHandlerSettings TransactionHandlerJob { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }
        public AssetsSettings Assets { get; set; }
        public ClientAccountSettings ClientAccountClient { get; set; }
        public EthereumSettings Ethereum { get; set; }
        public BitcoinCoreSettings BitCoinCore { get; set; }
        public MatchingEngineSettings MatchingEngineClient { get; set; }
        public NotificationsSettings AppNotifications { get; set; }
        public RabbitMqSettings RabbitMq { get; set; }
        public EthRabbitMqSettings EthRabbitMq { get; set; }
        public PersonalDataServiceClientSettings PersonalDataServiceSettings { get; set; }
        public OperationsRepositoryServiceClientSettings OperationsRepositoryServiceClient { get; set; }

        public class TransactionHandlerSettings
        {
            public DbSettings Db { get; set; }
            public AssetsCacheSettings AssetsCache { get; set; }
            public string ExchangeOperationsServiceUrl { get; set; }
            public ServiceSettings Services { get; set; }
            public string QueuePostfix { get; set; }
            public long RetryDelayInMilliseconds { get; set; }
            public string SagasRabbitMqConnStr { get; set; }
            [Optional]
            public ChaosSettings ChaosKitty { get; set; }
            public MongoDeduplicatorSettings MongoDeduplicator { get; set; }
        }

        public class ChaosSettings
        {
            public double StateOfChaos { get; set; }
        }

        public class MongoDeduplicatorSettings
        {
            public string ConnectionString { get; set; }
            public string CollectionName { get; set; }
        }

        public class DbSettings
        {
            public string LogsConnString { get; set; }
            public string BitCoinQueueConnectionString { get; set; }
            public string ClientPersonalInfoConnString { get; set; }
            public string BalancesInfoConnString { get; set; }
            public string LwEthLogsConnString { get; set; }
            public string HMarketOrdersConnString { get; set; }
            public string OffchainConnString { get; set; }
            public string SolarCoinConnString { get; set; }
            public string FeeLogsConnString { get; set; }
        }

        public class AssetsCacheSettings
        {
            public TimeSpan ExpirationPeriod { get; set; }
        }

        public class NotificationsSettings
        {
            public string HubConnString { get; set; }
            public string HubName { get; set; }
        }

        public class MatchingEngineSettings
        {
            public IpEndpointSettings IpEndpoint { get; set; }
        }

        public class IpEndpointSettings
        {
            public string InternalHost { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }

            public IPEndPoint GetClientIpEndPoint(bool useInternal = false)
            {
                var host = useInternal ? InternalHost : Host;

                if (IPAddress.TryParse(host, out var ipAddress))
                    return new IPEndPoint(ipAddress, Port);

                var addresses = Dns.GetHostAddressesAsync(host).Result;
                return new IPEndPoint(addresses[0], Port);
            }
        }

        public class BitcoinCoreSettings
        {
            public string BitcoinCoreApiUrl { get; set; }
        }

        public class SlackIntegrationSettings
        {
            public class Channel
            {
                public string Type { get; set; }
                public string WebHookUrl { get; set; }
            }

            public string Env { get; set; }
            public Channel[] Channels { get; set; }

            public string GetChannelWebHook(string type)
            {
                return Channels.FirstOrDefault(x => x.Type == type)?.WebHookUrl;
            }
        }

        public class EthereumSettings
        {
            public string EthereumCoreUrl { get; set; }
            public string HotwalletAddress { get; set; }
        }

        public class EthRabbitMqSettings
        {
            public string ConnectionString { get; set; }
            public string ExchangeEthereumEvents { get; set; }
        }

        public class RabbitMqSettings
        {
            public string ConnectionString { get; set; }
            public string AlternateConnectionString { get; set; }
            public string ExchangeSwap { get; set; }
            public string ExchangeLimit { get; set; }

            public string ExchangeCashOperation { get; set; }
            public string ExchangeTransfer { get; set; }
        }

        public class SlackNotificationsSettings
        {
            public AzureQueueSettings AzureQueue { get; set; }
        }

        public class AzureQueueSettings
        {
            public string ConnectionString { get; set; }

            public string QueueName { get; set; }
        }

        public class AssetsSettings
        {
            public string ServiceUrl { get; set; }
        }

        public class ClientAccountSettings
        {
            public string ServiceUrl { get; set; }
        }
    }

    public class ServiceSettings
    {
        public string OperationsUrl { get; set; }
    }
}
