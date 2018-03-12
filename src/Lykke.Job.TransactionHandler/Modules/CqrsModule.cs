using System;
using System.Collections.Generic;
using Autofac;
using Common.Log;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Commands.EthereumCore;
using Lykke.Job.TransactionHandler.Commands.LimitTrades;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Events.EthereumCore;
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
            builder.RegisterType<TransferSaga>();
            builder.RegisterType<EthereumCoreSaga>();

            builder.RegisterType<ForwardWithdrawalCommandHandler>();
            builder.RegisterType<BitcoinCommandHandler>()
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(longRetryDelay)));
            builder.RegisterType<ChronoBankCommandHandler>();
            builder.RegisterType<EthereumCommandHandler>()
                .WithParameter(TypedParameter.From(_settings.Ethereum))
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(longRetryDelay)));
            builder.RegisterType<SolarCoinCommandHandler>();
            builder.RegisterType<OffchainCommandHandler>();
            builder.RegisterType<OperationsCommandHandler>()
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(longRetryDelay)));
            builder.RegisterType<TradeCommandHandler>();

            builder.RegisterType<HistoryCommandHandler>();
            builder.RegisterType<LimitOrderCommandHandler>();
            builder.RegisterType<NotificationsCommandHandler>();
            builder.RegisterType<EthereumCoreCommandHandler>();

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
                    .WithProjection(typeof(FeeProjection), BoundedContexts.Trades)
                    .ListeningEvents(typeof(TransferOperationStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .WithProjection(typeof(FeeProjection), BoundedContexts.Operations),

                Register.Saga<TradeSaga>($"{BoundedContexts.TxHandler}.trade-saga")
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .PublishingCommands(typeof(CreateTransactionCommand))
                        .To(BoundedContexts.Trades).With(defaultPipeline),

                Register.BoundedContext(BoundedContexts.TxHandler)
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
                    .ListeningCommands(typeof(ProcessEthereumCashoutCommand), typeof(TransferEthereumCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(EthereumTransferSentEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<EthereumCommandHandler>(),

                Register.BoundedContext(BoundedContexts.EthereumCommands)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ProcessCoinEventCommand),
                                       typeof(ProcessHotWalletEventCommand))
                        .On(defaultRoute) 
                        .WithLoopback(defaultRoute)
                    .ListeningCommands(typeof(EnrollEthCashinToMatchingEngineCommand),
                                       typeof(SaveEthInHistoryCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(CashinDetectedEvent),
                                      typeof(EthCashinEnrolledToMatchingEngineEvent),
                                      typeof(EthCashinSavedInHistoryEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<EthereumCoreCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Offchain)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateOffchainCashoutRequestCommand), typeof(CreateOffchainCashinRequestCommand))
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
                            typeof(SaveTransferOperationStateCommand),
                            typeof(SaveManualOperationStateCommand),
                            typeof(SaveCashoutOperationStateCommand),
                            typeof(SaveIssueOperationStateCommand),
                            typeof(CompleteOperationCommand))
                        .On(defaultRoute)
                    .PublishingEvents(
                            typeof(TransferOperationStateSavedEvent),
                            typeof(ManualTransactionStateSavedEvent),
                            typeof(IssueTransactionStateSavedEvent),
                            typeof(CashoutTransactionStateSavedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<OperationsCommandHandler>(),

                Register.BoundedContext(BoundedContexts.OperationsHistory)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningEvents(
                            typeof(TransferOperationStateSavedEvent),
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
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.TxHandler).On(defaultRoute)
                        .WithProjection(typeof(OperationHistoryProjection), BoundedContexts.TxHandler)
                        .ProcessingOptions(defaultRoute).MultiThreaded(4).QueueCapacity(1024)
                    .ListeningCommands(typeof(UpdateLimitOrdersCountCommand))
                        .On(defaultPipeline)
                        .WithCommandsHandler<HistoryCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Email)
                    .ListeningEvents(typeof(SolarCashOutCompletedEvent))
                        .From(BoundedContexts.Solarcoin).On(defaultRoute)
                    .WithProjection(typeof(EmailProjection), BoundedContexts.Solarcoin),

                Register.BoundedContext(BoundedContexts.Push)
                    .ListeningCommands(typeof(LimitTradeNotifySendCommand)).On(defaultPipeline)
                        .WithCommandsHandler<NotificationsCommandHandler>(),

                Register.Saga<CashInOutSaga>($"{BoundedContexts.TxHandler}.cash-out-saga")
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

                Register.Saga<ForwardWithdawalSaga>($"{BoundedContexts.TxHandler}.forward-withdrawal-saga")
                    .ListeningEvents(typeof(CashoutTransactionStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .PublishingCommands(typeof(SetLinkedCashInOperationCommand))
                        .To(BoundedContexts.ForwardWithdrawal).With(defaultPipeline),

                Register.Saga<HistorySaga>("history-saga")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.TxHandler).On("events")
                    .PublishingCommands(typeof(UpdateLimitOrdersCountCommand))
                        .To(BoundedContexts.OperationsHistory).With(defaultPipeline),

                Register.Saga<NotificationsSaga>("notifications-saga")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.TxHandler).On(defaultRoute)
                    .PublishingCommands(typeof(LimitTradeNotifySendCommand))
                        .To(BoundedContexts.Push).With(defaultPipeline),

                Register.Saga<TransferSaga>("transfers-saga")
                    .ListeningEvents(typeof(TransferOperationStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .ListeningEvents(typeof(EthereumTransferSentEvent))
                        .From(BoundedContexts.Ethereum).On(defaultRoute)
                    .PublishingCommands(typeof(TransferEthereumCommand))
                        .To(BoundedContexts.Ethereum).With(defaultPipeline)
                    .PublishingCommands(typeof(CompleteOperationCommand))
                        .To(BoundedContexts.Operations).With(defaultPipeline)
                    .PublishingCommands(typeof(CreateOffchainCashinRequestCommand))
                        .To(BoundedContexts.Offchain).With(defaultPipeline),

                Register.Saga<EthereumCoreSaga>("ethereum-core-saga")
                    .ListeningEvents(typeof(CashinDetectedEvent),
                                     typeof(EthCashinEnrolledToMatchingEngineEvent),
                                     typeof(EthCashinSavedInHistoryEvent))
                        .From(BoundedContexts.EthereumCommands).On(defaultRoute)
                    .PublishingCommands(typeof(EnrollEthCashinToMatchingEngineCommand),
                                        typeof(SaveEthInHistoryCommand))
                        .To(BoundedContexts.EthereumCommands).With(defaultPipeline),

                Register.DefaultRouting
                    .PublishingCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .To(BoundedContexts.Offchain).With(defaultPipeline)
                    .PublishingCommands(typeof(SegwitTransferCommand))
                        .To(BoundedContexts.Bitcoin).With(defaultPipeline)
                    .PublishingCommands(typeof(SaveManualOperationStateCommand), typeof(SaveIssueOperationStateCommand), typeof(SaveCashoutOperationStateCommand))
                        .To(BoundedContexts.Operations).With(defaultPipeline)
                    .PublishingCommands(typeof(CreateTradeCommand))
                        .To(BoundedContexts.Trades).With(defaultPipeline)
                    .PublishingCommands(typeof(SaveTransferOperationStateCommand))
                        .To(BoundedContexts.Operations).With(defaultPipeline)
                );
            })
            .As<ICqrsEngine>().SingleInstance();
        }
    }
}
