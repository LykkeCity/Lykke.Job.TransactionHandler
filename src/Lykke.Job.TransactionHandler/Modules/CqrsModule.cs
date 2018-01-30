using System;
using System.Collections.Generic;
using Autofac;
using Common.Log;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Handlers;
using Lykke.Job.TransactionHandler.Projections;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Sagas;
using Lykke.Job.TransactionHandler.Sagas.Services;
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

            Inceptum.Messaging.Serialization.MessagePackSerializerFactory.Defaults.FormatterResolver = MessagePack.Resolvers.ContractlessStandardResolver.Instance;

            builder.Register(context => new AutofacDependencyResolver(context)).As<IDependencyResolver>().SingleInstance();

            var rabbitMqSettings = new RabbitMQ.Client.ConnectionFactory { Uri = _settings.TransactionHandlerJob.SagasRabbitMqConnStr };
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

            var defaultRetryDelay = _settings.TransactionHandlerJob.RetryDelayInMilliseconds;
            var longRetryDelay = defaultRetryDelay * 60;

            builder.RegisterType<CashInOutMessageProcessor>();
            builder.RegisterType<CashInOutSaga>();
            builder.RegisterType<ForwardWithdawalSaga>();
            builder.RegisterType<HistorySaga>();
            builder.RegisterType<NotificationsSaga>();
            builder.RegisterType<TradeSaga>();

            builder.RegisterType<ForwardWithdrawalCommandHandler>();
            builder.RegisterType<BitcoinCommandHandler>()
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(longRetryDelay)));
            builder.RegisterType<ChronoBankCommandHandler>();
            builder.RegisterType<EthereumCommandHandler>()
                .WithParameter(TypedParameter.From(_settings.Ethereum))
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(longRetryDelay)));
            builder.RegisterType<SolarCoinCommandHandler>();
            builder.RegisterType<OffchainCommandHandler>();
            builder.RegisterType<OperationsCommandHandler>();
            builder.RegisterType<TradeCommandHandler>();

            builder.RegisterType<HistoryCommandHandler>();
            builder.RegisterType<LimitOrderCommandHandler>();
            builder.RegisterType<NotificationsCommandHandler>();

            builder.RegisterType<ClientTradesProjection>();
            builder.RegisterType<ContextProjection>();
            builder.RegisterType<FeeLogsProjection>();
            builder.RegisterType<LimitOrdersProjection>();
            builder.RegisterType<LimitTradeEventsProjection>();
            builder.RegisterType<OperationHistoryProjection>();
            builder.RegisterType<EmailProjection>();
            builder.RegisterType<OrdersProjection>();
            builder.RegisterType<FeeProjection>();

            builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
            builder.RegisterType<ClientTradesFactory>().As<IClientTradesFactory>().SingleInstance();

            builder.Register(ctx =>
            {
                const string defaultPipeline = "commands";
                const string defaultRoute = "self";

                return new CqrsEngine(_log,
                    ctx.Resolve<IDependencyResolver>(),
                    messagingEngine,
                    new DefaultEndpointProvider(),
                    true,
                    Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver(
                        "RabbitMq",
                        "messagepack",
                        environment: "lykke",
                        exclusiveQueuePostfix: _settings.TransactionHandlerJob.QueuePostfix)),

                Register.BoundedContext(BoundedContexts.Trades)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateTradeCommand), typeof(CreateTransactionCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(TradeCreatedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<TradeCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Orders)
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .WithProjection(typeof(OrdersProjection), BoundedContexts.Trades),

                Register.BoundedContext(BoundedContexts.Fee)
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .WithProjection(typeof(FeeProjection), BoundedContexts.Trades),

                Register.Saga<TradeSaga>($"{BoundedContexts.Self}.trade-saga")
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .PublishingCommands(typeof(CreateTransactionCommand))
                        .To(BoundedContexts.Trades).With(defaultPipeline),

                Register.BoundedContext(BoundedContexts.Self)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ProcessLimitOrderCommand))
                        .On(defaultPipeline)
                        .WithLoopback()
                    .PublishingEvents(typeof(LimitOrderExecutedEvent))
                        .With("events")
                    .WithCommandsHandler<LimitOrderCommandHandler>(),

                Register.BoundedContext(BoundedContexts.ForwardWithdrawal)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SetLinkedCashInOperationCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(ForwardWithdawalLinkedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<ForwardWithdrawalCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Bitcoin)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(BitcoinCashOutCommand), typeof(SegwitTransferCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<BitcoinCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Chronobank)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ChronoBankCashOutCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<ChronoBankCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Ethereum)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ProcessEthereumCashoutCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<EthereumCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Offchain)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<OffchainCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Solarcoin)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SolarCashOutCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(SolarCashOutCompletedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<SolarCoinCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Operations)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(
                            typeof(SaveManualOperationStateCommand),
                            typeof(SaveCashoutOperationStateCommand),
                            typeof(SaveIssueOperationStateCommand))
                        .On(defaultRoute)
                    .PublishingEvents(
                            typeof(ManualTransactionStateSavedEvent),
                            typeof(IssueTransactionStateSavedEvent),
                            typeof(CashoutTransactionStateSavedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<OperationsCommandHandler>(),

                Register.BoundedContext(BoundedContexts.OperationsHistory)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningEvents(
                            typeof(ManualTransactionStateSavedEvent),
                            typeof(IssueTransactionStateSavedEvent),
                            typeof(CashoutTransactionStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .WithProjection(typeof(OperationHistoryProjection), BoundedContexts.Operations)
                    .ListeningEvents(typeof(ForwardWithdawalLinkedEvent))
                        .From(BoundedContexts.ForwardWithdrawal).On(defaultRoute)
                    .WithProjection(typeof(OperationHistoryProjection), BoundedContexts.ForwardWithdrawal)
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .WithProjection(typeof(OperationHistoryProjection), BoundedContexts.Trades)
                    .ListeningEvents(typeof(LimitOrderSavedEvent))
                        .From(BoundedContexts.OperationsHistory).On("client-cache")
                        .WithProjection(typeof(OperationHistoryProjection), BoundedContexts.OperationsHistory)
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.Self).On("limit-orders")
                        .WithProjection(typeof(LimitOrdersProjection), BoundedContexts.Self)
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.Self).On("limit-trade-events")
                        .WithProjection(typeof(LimitTradeEventsProjection), BoundedContexts.Self)
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.Self).On("fee")
                        .WithProjection(typeof(FeeLogsProjection), BoundedContexts.Self)
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.Self).On("operations-context")
                        .WithProjection(typeof(ContextProjection), BoundedContexts.Self)
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.Self).On("client-trades")
                        .WithProjection(typeof(ClientTradesProjection), BoundedContexts.Self)
                    .ListeningCommands(typeof(CreateOrUpdateLimitOrderCommand))
                        .On(defaultPipeline)
                        .WithCommandsHandler<HistoryCommandHandler>()
                    .PublishingEvents(typeof(LimitOrderSavedEvent))
                        .With("events"),

                Register.BoundedContext(BoundedContexts.Email)
                    .ListeningEvents(typeof(SolarCashOutCompletedEvent))
                        .From(BoundedContexts.Solarcoin).On(defaultRoute)
                    .WithProjection(typeof(EmailProjection), BoundedContexts.Solarcoin),

                Register.BoundedContext(BoundedContexts.Push)
                        .ListeningCommands(typeof(LimitTradeNotifySendCommand)).On(defaultPipeline)
                        .WithCommandsHandler<NotificationsCommandHandler>(),

                Register.Saga<CashInOutSaga>($"{BoundedContexts.Self}.cash-out-saga")
                    .ListeningEvents(typeof(CashoutTransactionStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .PublishingCommands(typeof(BitcoinCashOutCommand))
                        .To(BoundedContexts.Bitcoin).With(defaultPipeline)
                    .PublishingCommands(typeof(ChronoBankCashOutCommand))
                        .To(BoundedContexts.Chronobank).With(defaultPipeline)
                    .PublishingCommands(typeof(ProcessEthereumCashoutCommand))
                        .To(BoundedContexts.Ethereum).With(defaultPipeline)
                    .PublishingCommands(typeof(SolarCashOutCommand))
                        .To(BoundedContexts.Solarcoin).With(defaultPipeline)
                    .PublishingCommands(typeof(BlockchainCashoutProcessor.Contract.Commands.StartCashoutCommand))
                        .To(BlockchainCashoutProcessor.Contract.BlockchainCashoutProcessorBoundedContext.Name).With(defaultPipeline),

                Register.Saga<ForwardWithdawalSaga>($"{BoundedContexts.Self}.forward-withdrawal-saga")
                    .ListeningEvents(typeof(CashoutTransactionStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .PublishingCommands(typeof(SetLinkedCashInOperationCommand))
                        .To(BoundedContexts.ForwardWithdrawal).With(defaultPipeline),

                Register.Saga<HistorySaga>("history-saga")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.Self).On("events")
                    .PublishingCommands(typeof(CreateOrUpdateLimitOrderCommand))
                        .To(BoundedContexts.OperationsHistory).With(defaultPipeline),

                Register.Saga<NotificationsSaga>("notifications-saga")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.Self).On(defaultRoute)
                    .PublishingCommands(typeof(LimitTradeNotifySendCommand))
                        .To(BoundedContexts.Push).With(defaultPipeline),

                Register.DefaultRouting
                    .PublishingCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .To(BoundedContexts.Offchain).With(defaultPipeline)
                    .PublishingCommands(typeof(SegwitTransferCommand))
                        .To(BoundedContexts.Bitcoin).With(defaultPipeline)
                    .PublishingCommands(typeof(SaveManualOperationStateCommand), typeof(SaveIssueOperationStateCommand), typeof(SaveCashoutOperationStateCommand))
                        .To(BoundedContexts.Operations).With(defaultPipeline)
                    .PublishingCommands(typeof(CreateTradeCommand))
                        .To(BoundedContexts.Trades).With(defaultPipeline)
                );
            })
            .As<ICqrsEngine>().SingleInstance();
        }
    }
}
