using System;
using System.Linq;
using System.Net;
using Lykke.Service.PersonalData.Settings;

namespace Lykke.Job.TransactionHandler.Services
{
    public class AppSettings
    {
        public TransactionHandlerSettings TransactionHandlerJob { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }
        public SlackIntegrationSettings SlackIntegration { get; set; }
        public AssetsSettings Assets { get; set; }
        public EthereumSettings Ethereum { get; set; }
        public BitcoinCoreSettings BitCoinCore { get; set; }
        public MarginTradingSettings MarginTrading { get; set; }
        public MatchingEngineSettings MatchingEngineClient { get; set; }
        public NotificationsSettings AppNotifications { get; set; }
        public RabbitMqSettings RabbitMq { get; set; }
        public PersonalDataServiceSettings PersonalDataServiceSettings { get; set; }

        public class TransactionHandlerSettings
        {
            public DbSettings Db { get; set; }
            public AssetsCacheSettings AssetsCache { get; set; }
            public string ExchangeOperationsServiceUrl { get; set; }
        }

        public class DbSettings
        {
            public string LogsConnString { get; set; }
            public string BitCoinQueueConnectionString { get; set; }
            public string DictsConnString { get; set; }
            public string ClientPersonalInfoConnString { get; set; }
            public string BalancesInfoConnString { get; set; }
            public string HTradesConnString { get; set; }
            public string ChronoBankSrvConnString { get; set; }
            public string LwEthLogsConnString { get; set; }
            public string HMarketOrdersConnString { get; set; }
            public string OffchainConnString { get; set; }
            public string QuantaSrvConnString { get; set; }
            public string SolarCoinConnString { get; set; }
            public string HistoryLogsConnString { get; set; }
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
                string host = useInternal ? InternalHost : Host;

                IPAddress ipAddress;
                if (IPAddress.TryParse(host, out ipAddress))
                    return new IPEndPoint(ipAddress, Port);

                var addresses = Dns.GetHostAddressesAsync(host).Result;
                return new IPEndPoint(addresses[0], Port);
            }
        }

        public class MarginTradingSettings
        {
            public string ApiKey { get; set; }
            public string DemoApiKey { get; set; }
            public string ApiRootUrl { get; set; }
            public string DemoApiRootUrl { get; set; }
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

        public class RabbitMqSettings
        {
            public string ConnectionString { get; set; }

            public string ExternalHost { get; set; }

            public int Port { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }

            public string ExchangeSwap { get; set; }
            public string ExchangeLimit { get; set; }

            public string ExchangeCashOperation { get; set; }
            public string ExchangeTransfer { get; set; }
            public string ExchangeSwapOperation { get; set; }

            public string ExchangeEthereumCashIn { get; set; }
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
    }
}