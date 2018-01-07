using System.Collections.Generic;
using Autofac;
using Common.Log;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Handlers;
using Lykke.Job.TransactionHandler.Projections;
using Lykke.Job.TransactionHandler.Sagas;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Messaging;
using Lykke.SettingsReader;

namespace Lykke.Job.TransactionHandler.Modules
{
    public class CqrsModule : Module
    {
        private readonly AppSettings _settings;
        private readonly ILog _log;

        public CqrsModule(IReloadingManager<AppSettings> settingsManager, ILog log)
        {
            _settings = settingsManager.CurrentValue;
            _log = log;
        }

        protected override void Load(ContainerBuilder builder)
        {
            if (_settings.TransactionHandlerJob.ChaosKitty != null)
            {
                ChaosKitty.StateOfChaos = _settings.TransactionHandlerJob.ChaosKitty.StateOfChaos;
            }

            InitSerializer();

            builder.Register(context => new AutofacDependencyResolver(context)).As<IDependencyResolver>().SingleInstance();

            var rabbitMqSettings = new RabbitMQ.Client.ConnectionFactory { Uri = _settings.RabbitMq.ConnectionString };
#if DEBUG
            var virtualHost = "/debug";
            var messagingEngine = new MessagingEngine(_log,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"RabbitMq", new TransportInfo(rabbitMqSettings.Endpoint + virtualHost, rabbitMqSettings.UserName, rabbitMqSettings.Password, "None", "RabbitMq")}
                }),
                new RabbitMqTransportFactory());
#else
            var messagingEngine = new MessagingEngine(_log,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"RabbitMq", new TransportInfo(rabbitMqSettings.Endpoint.ToString(), rabbitMqSettings.UserName, rabbitMqSettings.Password, "None", "RabbitMq")}
                }),
                new RabbitMqTransportFactory());
#endif

            builder.RegisterType<CashInOutMessageProcessor>();
            builder.RegisterType<CashInOutSaga>();
            builder.RegisterType<ForwardWithdawalSaga>();

            builder.RegisterType<ForwardWithdrawalCommandHandler>();
            builder.RegisterType<BitcoinCommandHandler>();
            builder.RegisterType<ChronoBankCommandHandler>();
            builder.RegisterType<EthereumCommandHandler>()
                .WithParameter(TypedParameter.From(_settings.Ethereum));
            builder.RegisterType<SolarCoinCommandHandler>();
            builder.RegisterType<OffchainCommandHandler>();
            builder.RegisterType<OperationsCommandHandler>();
            builder.RegisterType<TransactionsCommandHandler>();

            builder.RegisterType<OperationHistoryProjection>();
            builder.RegisterType<NotificationsProjection>();

            var defaultRetryDelay = _settings.TransactionHandlerJob.RetryDelayInMilliseconds;
            builder.Register(ctx =>
            {
                var historyProjection = ctx.Resolve<OperationHistoryProjection>();
                var notificationsProjection = ctx.Resolve<NotificationsProjection>();

                return new CqrsEngine(_log,
                    ctx.Resolve<IDependencyResolver>(),
                    messagingEngine,
                    new DefaultEndpointProvider(),
                    true,
                    Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("RabbitMq", "protobuf", environment: _settings.TransactionHandlerJob.Environment)),

                Register.BoundedContext("tx-handler"),

                Register.BoundedContext("forward-withdrawal")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SetLinkedCashInOperationCommand))
                        .On("forward-withdrawal-commands")
                    .PublishingEvents(typeof(ForwardWithdawalLinkedEvent))
                        .With("forward-withdrawal-events")
                    .WithCommandsHandler<ForwardWithdrawalCommandHandler>(),

                Register.BoundedContext("bitcoin")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SendBitcoinCommand), typeof(BitcoinCashOutCommand))
                        .On("bitcoin-commands")
                    .WithCommandsHandler<BitcoinCommandHandler>(),

                Register.BoundedContext("chronobank")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ChronoBankCashOutCommand))
                        .On("chronobank-commands")
                    .WithCommandsHandler<ChronoBankCommandHandler>(),

                Register.BoundedContext("ethereum")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ProcessEthereumCashoutCommand))
                        .On("ethereum-commands")
                    .WithCommandsHandler<EthereumCommandHandler>(),

                Register.BoundedContext("offchain")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .On("offchain-commands")
                    .WithCommandsHandler<OffchainCommandHandler>(),

                Register.BoundedContext("solarcoin")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SolarCashOutCommand))
                        .On("solarcoin-commands")
                    .PublishingEvents(typeof(SolarCashOutCompletedEvent))
                        .With("solarcoin-events")
                    .WithCommandsHandler<SolarCoinCommandHandler>(),

                Register.BoundedContext("operations")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(RegisterCashInOutOperationCommand))
                        .On("operations-commands")
                    .WithCommandsHandler<OperationsCommandHandler>(),

                Register.BoundedContext("transactions")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SaveCashoutTransactionStateCommand), typeof(SaveDestroyTransactionStateCommand), typeof(SaveIssueTransactionStateCommand))
                        .On("transactions-commands")
                    .PublishingEvents(typeof(IssueTransactionStateSavedEvent), typeof(DestroyTransactionStateSavedEvent), typeof(CashoutTransactionStateSavedEvent))
                        .With("transactions-events")
                    .WithCommandsHandler<TransactionsCommandHandler>(),

                Register.BoundedContext("history")
                    .ListeningEvents(typeof(IssueTransactionStateSavedEvent), typeof(DestroyTransactionStateSavedEvent),
                            typeof(CashoutTransactionStateSavedEvent))
                        .From("transactions").On("transactions-events")
                    .WithProjection(historyProjection, "transactions")
                    .ListeningEvents(typeof(ForwardWithdawalLinkedEvent))
                        .From("forward-withdrawal").On("forward-withdrawal-events")
                    .WithProjection(historyProjection, "forward-withdrawal"),

                Register.BoundedContext("notifications")
                    .ListeningEvents(typeof(SolarCashOutCompletedEvent))
                        .From("solarcoin").On("solarcoin-events")
                    .WithProjection(notificationsProjection, "solarcoin"),

                Register.Saga<CashInOutSaga>("cash-out-saga")
                    .ListeningEvents(typeof(DestroyTransactionStateSavedEvent), typeof(CashoutTransactionStateSavedEvent))
                        .From("transactions").On("transactions-events")
                    .PublishingCommands(typeof(SendBitcoinCommand), typeof(BitcoinCashOutCommand))
                        .To("bitcoin").With("bitcoin-commands")
                    .PublishingCommands(typeof(ChronoBankCashOutCommand))
                        .To("chronobank").With("chronobank-commands")
                    .PublishingCommands(typeof(ProcessEthereumCashoutCommand))
                        .To("ethereum").With("ethereum-commands")
                    .PublishingCommands(typeof(SolarCashOutCommand))
                        .To("solarcoin").With("solarcoin-commands"),

                    Register.Saga<ForwardWithdawalSaga>("forward-withdrawal-saga")
                        .ListeningEvents(typeof(CashoutTransactionStateSavedEvent))
                            .From("transactions").On("transactions-events")
                        .PublishingCommands(typeof(SetLinkedCashInOperationCommand))
                            .To("forward-withdrawal").With("forward-withdrawal-commands"),

                Register.DefaultRouting
                    .PublishingCommands(typeof(SendBitcoinCommand), typeof(BitcoinCashOutCommand))
                        .To("bitcoin").With("bitcoin-commands")
                    .PublishingCommands(typeof(ChronoBankCashOutCommand))
                        .To("chronobank").With("chronobank-commands")
                    .PublishingCommands(typeof(ProcessEthereumCashoutCommand))
                        .To("ethereum").With("ethereum-commands")
                    .PublishingCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .To("offchain").With("offchain-commands")
                    .PublishingCommands(typeof(RegisterCashInOutOperationCommand))
                        .To("operations").With("operations-commands")
                    .PublishingCommands(typeof(SaveIssueTransactionStateCommand), typeof(SaveDestroyTransactionStateCommand), typeof(SaveCashoutTransactionStateCommand))
                        .To("transactions").With("transactions-commands")
                    .PublishingCommands(typeof(SetLinkedCashInOperationCommand))
                        .To("forward-withdrawal").With("forward-withdrawal-commands")
                    .PublishingCommands(typeof(SolarCashOutCommand))
                        .To("solarcoin").With("solarcoin-commands")
                );
            })
            .As<ICqrsEngine>().SingleInstance();
        }

        private void InitSerializer()
        {
            SerializerBuilder.Build<Service.Assets.Client.Models.Asset>();
            SerializerBuilder.Build<Core.Domain.BitCoin.CashOutCommand>();
            SerializerBuilder.Build<Core.Domain.BitCoin.CashOutContextData>();
            SerializerBuilder.Build<Core.Domain.BitCoin.DestroyCommand>();
            SerializerBuilder.Build<Core.Domain.BitCoin.UncolorContextData>();
            SerializerBuilder.Build<Core.Domain.BitCoin.IssueCommand>();
            SerializerBuilder.Build<Core.Domain.BitCoin.IssueContextData>();
        }
    }
}
