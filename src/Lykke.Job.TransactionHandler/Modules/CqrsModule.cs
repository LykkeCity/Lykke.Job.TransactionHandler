using System;
using System.Collections.Generic;
using Autofac;
using Common.Log;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Commands.Ethereum;
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

            builder.Register(context => new AutofacDependencyResolver(context)).As<IDependencyResolver>().SingleInstance();

            Inceptum.Messaging.Serialization.MessagePackSerializerFactory.Defaults.FormatterResolver = MessagePack.Resolvers.ContractlessStandardResolver.Instance;

            var rabbitMqSettings = new RabbitMQ.Client.ConnectionFactory { Uri = _settings.TransactionHandlerJob.SagasRabbitMqConnStr };
#if DEBUG
            var virtualHost = "/debugX";
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
            builder.RegisterType<TradeSaga>();
            builder.RegisterType<TransferSaga>();
            builder.RegisterType<HistorySaga>();
            builder.RegisterType<NotificationsSaga>();

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

            builder.RegisterType<OperationHistoryProjection>();
            builder.RegisterType<TransactionsCommandHandler>();

            builder.RegisterType<OperationHistoryProjection>();
            builder.RegisterType<NotificationsProjection>();
            
            builder.RegisterType<TradeSaga>();
            builder.RegisterType<TransferSaga>();

            builder.RegisterType<TradeCommandHandler>();
            builder.RegisterType<TransferCommandHandler>();

            builder.RegisterType<HistoryProjection>();
            builder.RegisterType<FeeProjection>();
            builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
            builder.RegisterType<ClientTradesFactory>().As<IClientTradesFactory>().SingleInstance();

            builder.RegisterType<OperationHistoryProjection>();
            builder.RegisterType<EmailProjection>();
            builder.RegisterType<OrdersProjection>();
            builder.RegisterType<FeeProjection>();
            builder.RegisterType<TransfersProjection>();

            builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
            builder.RegisterType<ClientTradesFactory>().As<IClientTradesFactory>().SingleInstance();

            builder.RegisterType<TransactionsCommandHandler>();
            builder.RegisterType<HistoryCommandHandler>();
            builder.RegisterType<LimitOrderCommandHandler>();
            builder.RegisterType<NotificationsCommandHandler>();

            builder.RegisterType<ClientTradesProjection>();
            builder.RegisterType<ContextProjection>();
            builder.RegisterType<FeeLogsProjection>();
            builder.RegisterType<LimitOrdersProjection>();
            builder.RegisterType<LimitTradeEventsProjection>();
            builder.RegisterType<NotificationsProjection>();   
            builder.RegisterType<OperationHistoryProjection>(); 

            builder.Register(ctx =>
            {
                const string defaultPipeline = "commands";
                const string defaultRoute = "Self";

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

                Register.BoundedContext(BoundedContexts.Self),

                Register.BoundedContext(BoundedContexts.Trades)
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateTradeCommand), typeof(CreateTransactionCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(TradeCreatedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<TradeCommandHandler>(),

                Register.BoundedContext("transfers")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateTransferCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(TransferCreatedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<TransferCommandHandler>(),

                Register.BoundedContext("tx-handler")
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
                    .ListeningCommands(typeof(ProcessEthereumCashoutCommand), typeof(EthTransferTrustedWalletCommand))
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
                            typeof(SaveTransferOperationStateCommand),
                            typeof(SaveManualOperationStateCommand),
                            typeof(SaveCashoutOperationStateCommand),
                            typeof(SaveIssueOperationStateCommand))
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
                    .WithProjection(typeof(OperationHistoryProjection), BoundedContexts.Trades),

                Register.BoundedContext("operations-history")
                    .ListeningEvents(typeof(IssueTransactionStateSavedEvent), typeof(DestroyTransactionStateSavedEvent), typeof(CashoutTransactionStateSavedEvent))
                        .From("transactions").On(defaultRoute)
                        .WithProjection(typeof(OperationHistoryProjection), "transactions")
                    .ListeningEvents(typeof(ForwardWithdawalLinkedEvent))
                        .From("forward-withdrawal").On(defaultRoute)
                        .WithProjection(typeof(OperationHistoryProjection), "forward-withdrawal")                                        
                    .ListeningEvents(typeof(LimitOrderSavedEvent))
                        .From("operations-history").On("client-cache")
                        .WithProjection(typeof(OperationHistoryProjection), "operations-history")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From("tx-handler").On("limit-orders")
                        .WithProjection(typeof(LimitOrdersProjection), "tx-handler")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From("tx-handler").On("limit-trade-events")
                        .WithProjection(typeof(LimitTradeEventsProjection), "tx-handler")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From("tx-handler").On("fee")
                        .WithProjection(typeof(FeeLogsProjection), "tx-handler")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From("tx-handler").On("operations-context")
                        .WithProjection(typeof(ContextProjection), "tx-handler")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From("tx-handler").On("client-trades")
                        .WithProjection(typeof(ClientTradesProjection), "tx-handler")
                    .ListeningCommands(typeof(CreateOrUpdateLimitOrderCommand))
                        .On(defaultPipeline)
                        .WithCommandsHandler<HistoryCommandHandler>()                    
                    .PublishingEvents(typeof(LimitOrderSavedEvent))
                        .With("events"),
                
                Register.BoundedContext(BoundedContexts.Orders)
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .WithProjection(typeof(OrdersProjection), BoundedContexts.Trades),

                Register.BoundedContext(BoundedContexts.Fee)
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .WithProjection(typeof(FeeProjection), BoundedContexts.Trades),

                Register.BoundedContext(BoundedContexts.Transfers)
                    .ListeningEvents(typeof(TransferOperationStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .WithProjection(typeof(TransfersProjection), BoundedContexts.Operations),

                Register.BoundedContext(BoundedContexts.Email)
                    .ListeningEvents(typeof(SolarCashOutCompletedEvent))
                        .From(BoundedContexts.Solarcoin).On(defaultRoute)
                    .WithProjection(typeof(EmailProjection), BoundedContexts.Solarcoin),

                Register.BoundedContext("push")                        
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

                Register.Saga<TradeSaga>($"{BoundedContexts.Self}.trade-saga")
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From(BoundedContexts.Trades).On(defaultRoute)
                    .PublishingCommands(typeof(CreateTransactionCommand))
                        .To(BoundedContexts.Trades).With(defaultPipeline),

                Register.Saga<TransferSaga>($"{BoundedContexts.Self}.transfers-saga")
                    .ListeningEvents(typeof(TransferOperationStateSavedEvent))
                        .From(BoundedContexts.Operations).On(defaultRoute)
                    .PublishingCommands(typeof(EthTransferTrustedWalletCommand))
                        .To(BoundedContexts.Ethereum).With(defaultPipeline),

                Register.Saga<HistorySaga>("history-saga")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From("tx-handler").On("events")                    
                    .PublishingCommands(typeof(CreateOrUpdateLimitOrderCommand))
                        .To("operations-history").With(defaultPipeline),    
                
                Register.Saga<NotificationsSaga>("notifications-saga")
                    .ListeningEvents(typeof(LimitOrderExecutedEvent))
                        .From("tx-handler").On(defaultRoute)
                    .PublishingCommands(typeof(LimitTradeNotifySendCommand))
                        .To("push").With(defaultPipeline),

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

                Register.DefaultRouting
                    .PublishingCommands(typeof(CreateOffchainCashoutRequestCommand))
                        .To("offchain").With(defaultPipeline)
                    .PublishingCommands(typeof(SegwitTransferCommand))
                        .To("bitcoin").With(defaultPipeline)
                    .PublishingCommands(typeof(RegisterCashInOutOperationCommand))
                        .To("operations").With(defaultPipeline)
                    .PublishingCommands(typeof(SaveIssueTransactionStateCommand), typeof(SaveDestroyTransactionStateCommand), typeof(SaveCashoutTransactionStateCommand))
                        .To("transactions").With(defaultPipeline)                    
                );
            })
            .As<ICqrsEngine>().SingleInstance();
        }
    }
}
