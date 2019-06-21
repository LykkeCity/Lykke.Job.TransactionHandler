using System;
using System.Collections.Generic;
using Autofac;
using Lykke.Common.Log;
using Lykke.Cqrs;
using Lykke.Cqrs.Configuration;
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
using Lykke.Job.TransactionHandler.Settings;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Messaging;
using Lykke.Messaging.RabbitMq;
using Lykke.Messaging.Serialization;
using Lykke.SettingsReader;

namespace Lykke.Job.TransactionHandler.Modules
{
    public class CqrsModule : Module
    {
        private readonly AppSettings _settings;

        public CqrsModule(IReloadingManager<AppSettings> appSettings)
        {
            _settings = appSettings.CurrentValue;
        }

        protected override void Load(ContainerBuilder builder)
        {
            if (_settings.TransactionHandlerJob.ChaosKitty != null)
            {
                ChaosKitty.StateOfChaos = _settings.TransactionHandlerJob.ChaosKitty.StateOfChaos;
            }

            MessagePackSerializerFactory.Defaults.FormatterResolver = MessagePack.Resolvers.ContractlessStandardResolver.Instance;

            builder.Register(context => new AutofacDependencyResolver(context)).As<IDependencyResolver>().SingleInstance();

            var rabbitMqSettings = new RabbitMQ.Client.ConnectionFactory { Uri = _settings.TransactionHandlerJob.SagasRabbitMqConnStr };

            builder.Register(ctx =>
            {
                var logFactory = ctx.Resolve<ILogFactory>();
                return new MessagingEngine(
                    logFactory,
                    new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {
                            "RabbitMq",
                            new TransportInfo(
                                rabbitMqSettings.Endpoint.ToString(),
                                rabbitMqSettings.UserName,
                                rabbitMqSettings.Password, "None", "RabbitMq")
                        }
                    }),
                    new RabbitMqTransportFactory(logFactory));
            });

            var defaultRetryDelay = _settings.TransactionHandlerJob.RetryDelayInMilliseconds;
            var longRetryDelay = defaultRetryDelay * 60;

            builder.RegisterType<CashInOutMessageProcessor>();
            builder.RegisterType<ForwardWithdawalSaga>();
            builder.RegisterType<HistorySaga>();
            builder.RegisterType<TradeSaga>();
            builder.RegisterType<TransferSaga>();
            builder.RegisterType<EthereumCoreSaga>();

            builder.RegisterType<ForwardWithdrawalCommandHandler>();
            builder.RegisterType<BitcoinCommandHandler>()
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(longRetryDelay))).SingleInstance();
            builder.RegisterType<EthereumCommandHandler>()
                .WithParameter(TypedParameter.From(_settings.Ethereum))
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(longRetryDelay)));
            builder.RegisterType<OffchainCommandHandler>();
            builder.RegisterType<OperationsCommandHandler>()
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(longRetryDelay)));
            builder.RegisterType<TradeCommandHandler>();

            builder.RegisterType<HistoryCommandHandler>();
            builder.RegisterType<LimitOrderCommandHandler>();
            builder.RegisterType<EthereumCoreCommandHandler>();

            builder.RegisterType<OperationHistoryProjection>();
            builder.RegisterType<OrdersProjection>();

            builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
            builder.RegisterType<ClientTradesFactory>().As<IClientTradesFactory>().SingleInstance();

            builder.Register(ctx =>
            {
                const string commandsRoute = "commands";
                const string eventsRoute = "events";
                const string defaultRoute = "self";

                var engine = new CqrsEngine(
                    ctx.Resolve<ILogFactory>(),
                    ctx.Resolve<IDependencyResolver>(),
                    ctx.Resolve<MessagingEngine>(),
                    new DefaultEndpointProvider(),
                    true,
                    Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver(
                        "RabbitMq",
                        SerializationFormat.MessagePack,
                        environment: "lykke",
                        exclusiveQueuePostfix: _settings.TransactionHandlerJob.QueuePostfix)),

                Register.BoundedContext(BoundedContexts.Trades)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateTradeCommand), typeof(CreateTransactionCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(TradeCreatedEvent))
                        .With(eventsRoute)
                    .WithCommandsHandler<TradeCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Orders)
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .WithProjection(typeof(OrdersProjection), BoundedContexts.Trades),

                Register.Saga<TradeSaga>($"{BoundedContexts.TxHandler}.trade-saga")
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .PublishingCommands(typeof(CreateTransactionCommand))
                        .To(BoundedContexts.Trades).With(commandsRoute),

                Register.BoundedContext(BoundedContexts.TxHandler)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ProcessLimitOrderCommand))
                        .On(commandsRoute)
                        .WithLoopback()
                    .PublishingEvents(typeof(LimitOrderExecutedEvent))
                        .With(eventsRoute)
                    .WithCommandsHandler<LimitOrderCommandHandler>(),

                Register.BoundedContext(BoundedContexts.ForwardWithdrawal)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SetLinkedCashInOperationCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(ForwardWithdawalLinkedEvent))
                        .With(eventsRoute)
                    .WithCommandsHandler<ForwardWithdrawalCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Bitcoin)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SegwitTransferCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<BitcoinCommandHandler>(),

                Register.BoundedContext(BoundedContexts.Ethereum)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(TransferEthereumCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(EthereumTransferSentEvent))
                        .With(eventsRoute)
                    .WithCommandsHandler<EthereumCommandHandler>(),

                Register.BoundedContext(BoundedContexts.EthereumCommands)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ProcessEthCoinEventCommand),
                                       typeof(ProcessHotWalletErc20EventCommand))
                        .On(defaultRoute)
                        .WithLoopback(defaultRoute)
                    .ListeningCommands(typeof(EnrollEthCashinToMatchingEngineCommand),
                                       typeof(SaveEthInHistoryCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(CashinDetectedEvent),
                                      typeof(EthCashinEnrolledToMatchingEngineEvent),
                                      typeof(EthCashinSavedInHistoryEvent))
                        .With(eventsRoute)
                    .WithCommandsHandler<EthereumCoreCommandHandler>()
                    .ProcessingOptions(defaultRoute).MultiThreaded(4).QueueCapacity(1024),

                Register.BoundedContext(BoundedContexts.Offchain)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<OffchainCommandHandler>(),

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
                        .With(eventsRoute)
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
                        .On(commandsRoute)
                        .WithCommandsHandler<HistoryCommandHandler>(),

                Register.Saga<ForwardWithdawalSaga>($"{BoundedContexts.TxHandler}.forward-withdrawal-saga")
                    .ListeningEvents(typeof(CashoutTransactionStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .PublishingCommands(typeof(SetLinkedCashInOperationCommand))
                        .To(BoundedContexts.ForwardWithdrawal).With(commandsRoute),

                Register.Saga<HistorySaga>("history-saga")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From(BoundedContexts.TxHandler).On("events")
                    .PublishingCommands(typeof(UpdateLimitOrdersCountCommand))
                        .To(BoundedContexts.OperationsHistory).With(commandsRoute),

                Register.Saga<TransferSaga>("transfers-saga")
                    .ListeningEvents(typeof(TransferOperationStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .ListeningEvents(typeof(EthereumTransferSentEvent))
                        .From(BoundedContexts.Ethereum).On(defaultRoute)
                    .PublishingCommands(typeof(TransferEthereumCommand))
                        .To(BoundedContexts.Ethereum).With(commandsRoute)
                    .PublishingCommands(typeof(CompleteOperationCommand))
                        .To(BoundedContexts.Operations).With(commandsRoute),

                Register.Saga<EthereumCoreSaga>("ethereum-core-saga")
                    .ListeningEvents(typeof(CashinDetectedEvent),
                                     typeof(EthCashinEnrolledToMatchingEngineEvent),
                                     typeof(EthCashinSavedInHistoryEvent))
                        .From(BoundedContexts.EthereumCommands).On(defaultRoute)
                    .PublishingCommands(typeof(EnrollEthCashinToMatchingEngineCommand),
                                        typeof(SaveEthInHistoryCommand))
                        .To(BoundedContexts.EthereumCommands).With(commandsRoute)
                        .ProcessingOptions(defaultRoute).MultiThreaded(4).QueueCapacity(1024),

                Register.DefaultRouting
                    .PublishingCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .To(BoundedContexts.Offchain).With(commandsRoute)
                    .PublishingCommands(typeof(SegwitTransferCommand))
                        .To(BoundedContexts.Bitcoin).With(commandsRoute)
                    .PublishingCommands(typeof(SaveManualOperationStateCommand), typeof(SaveIssueOperationStateCommand), typeof(SaveCashoutOperationStateCommand))
                        .To(BoundedContexts.Operations).With(commandsRoute)
                    .PublishingCommands(typeof(CreateTradeCommand))
                        .To(BoundedContexts.Trades).With(commandsRoute)
                    .PublishingCommands(typeof(SaveTransferOperationStateCommand))
                        .To(BoundedContexts.Operations).With(commandsRoute)
                );
                engine.StartPublishers();
                return engine;
            })
            .As<ICqrsEngine>()
            .AutoActivate()
            .SingleInstance();
        }
    }
}
