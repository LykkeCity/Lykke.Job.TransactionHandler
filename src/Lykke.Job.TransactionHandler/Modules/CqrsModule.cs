using System;
using System.Collections.Generic;
using Autofac;
using AzureStorage.Queue;
using Common.Log;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Commands.Bitcoin;
using Lykke.Job.TransactionHandler.Commands.Ethereum;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Events.Bitcoin;
using Lykke.Job.TransactionHandler.Events.Ethereum;
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
        private readonly IReloadingManager<AppSettings.DbSettings> _dbSettingsManager;

        public CqrsModule(IReloadingManager<AppSettings> settingsManager, ILog log)
        {
            _dbSettingsManager = settingsManager.Nested(x => x.TransactionHandlerJob.Db);
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

            builder.RegisterType<CashInOutMessageProcessor>();
            builder.RegisterType<CashInOutSaga>();
            builder.RegisterType<ForwardWithdawalSaga>();
            builder.RegisterType<HistorySaga>();
            builder.RegisterType<NotificationsSaga>();

            builder.RegisterType<ForwardWithdrawalCommandHandler>();
            builder.RegisterType<BitcoinCommandHandler>()
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(defaultRetryDelay)))
                .WithParameter(TypedParameter.From(AzureQueueExt.Create(_dbSettingsManager.ConnectionString(x => x.BitCoinQueueConnectionString), "intransactions")));
            builder.RegisterType<ChronoBankCommandHandler>();
            builder.RegisterType<EthereumCommandHandler>()
                .WithParameter(TypedParameter.From(_settings.Ethereum))
                .WithParameter(TypedParameter.From(TimeSpan.FromMilliseconds(defaultRetryDelay)));
            builder.RegisterType<SolarCoinCommandHandler>();
            builder.RegisterType<OffchainCommandHandler>();
            builder.RegisterType<OperationsCommandHandler>();
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

            // todo: check
            builder.RegisterType<TradeSaga>();
            builder.RegisterType<TransferSaga>();

            builder.RegisterType<TradeCommandHandler>();
            builder.RegisterType<TransferCommandHandler>();
            builder.RegisterType<NotificationsCommandHandler>();

            builder.RegisterType<HistoryProjection>();
            builder.RegisterType<FeeProjection>();
            builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
            builder.RegisterType<ClientTradesFactory>().As<IClientTradesFactory>().SingleInstance();

            builder.Register(ctx =>
            {
                var defaultPipeline = "commands";
                var defaultRoute = "self";
                var operationHistoryProjection = ctx.Resolve<OperationHistoryProjection>();
                var notificationsProjection = ctx.Resolve<NotificationsProjection>();
                var historyProjection = ctx.Resolve<HistoryProjection>();
                var feeProjection = ctx.Resolve<FeeProjection>();

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

                Register.BoundedContext("tx-handler")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ProcessLimitOrderCommand))
                        .On(defaultPipeline)
                        .WithLoopback()
                    .PublishingEvents(typeof(LimitOrderExecutedEvent))
                        .With("events")
                    .WithCommandsHandler<LimitOrderCommandHandler>(),

                Register.BoundedContext("tx-handler")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateTradeCommand), typeof(CreateTransactionCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(TradeCreatedEvent), typeof(TransactionCreatedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<TradeCommandHandler>(),

                Register.BoundedContext("transfers")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateTransferCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(TransferCreatedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<TransferCommandHandler>(),

                Register.BoundedContext("forward-withdrawal")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SetLinkedCashInOperationCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(ForwardWithdawalLinkedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<ForwardWithdrawalCommandHandler>(),

                Register.BoundedContext("bitcoin")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SendBitcoinCommand), typeof(BitcoinCashOutCommand), typeof(SegwitTransferCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<BitcoinCommandHandler>(),

                Register.BoundedContext("chronobank")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ChronoBankCashOutCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<ChronoBankCommandHandler>(),

                Register.BoundedContext("ethereum")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(ProcessEthereumCashoutCommand), typeof(EthCreateTransactionRequestCommand), typeof(EthGuaranteeTransferCommand), typeof(EthBuyCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(EthTransactionRequestCreatedEvent), typeof(EthGuaranteeTransferCompletedEvent), typeof(EthTransferCompletedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<EthereumCommandHandler>(),

                Register.BoundedContext("offchain")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateOffchainCashoutRequestCommand), typeof(TransferFromHubCommand), typeof(ReturnCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(OffchainRequestCreatedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<OffchainCommandHandler>(),

                Register.BoundedContext("solarcoin")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SolarCashOutCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(SolarCashOutCompletedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<SolarCoinCommandHandler>(),

                Register.BoundedContext("operations")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(RegisterCashInOutOperationCommand))
                        .On(defaultRoute)
                    .WithCommandsHandler<OperationsCommandHandler>(),

                Register.BoundedContext("transactions")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(SaveCashoutTransactionStateCommand), typeof(SaveDestroyTransactionStateCommand), typeof(SaveIssueTransactionStateCommand))
                        .On(defaultRoute)
                    .PublishingEvents(typeof(IssueTransactionStateSavedEvent), typeof(DestroyTransactionStateSavedEvent), typeof(CashoutTransactionStateSavedEvent))
                        .With(defaultPipeline)
                    .WithCommandsHandler<TransactionsCommandHandler>(),

                Register.BoundedContext("history")
                    .ListeningEvents(typeof(IssueTransactionStateSavedEvent), typeof(DestroyTransactionStateSavedEvent),
                            typeof(CashoutTransactionStateSavedEvent))
                        .From("transactions").On(defaultRoute)
                    .WithProjection(operationHistoryProjection, "transactions")
                    .ListeningEvents(typeof(ForwardWithdawalLinkedEvent))
                        .From("forward-withdrawal").On(defaultRoute)
                    .WithProjection(operationHistoryProjection, "forward-withdrawal")
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From("tx-handler").On(defaultRoute)
                    .WithProjection(historyProjection, "tx-handler")
                    .ListeningEvents(typeof(TradeCreatedEvent))
                        .From("tx-handler").On(defaultRoute)
                    .WithProjection(feeProjection, "tx-handler"),

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
                
                Register.BoundedContext("notifications")
                    .ListeningEvents(typeof(SolarCashOutCompletedEvent))
                        .From("solarcoin").On(defaultRoute)
                    .WithProjection(typeof(NotificationsProjection), "solarcoin"),

                Register.BoundedContext("push")                        
                        .ListeningCommands(typeof(LimitTradeNotifySendCommand)).On(defaultPipeline)
                        .WithCommandsHandler<NotificationsCommandHandler>(),

                Register.Saga<CashInOutSaga>("cash-out-saga")
                    .ListeningEvents(typeof(DestroyTransactionStateSavedEvent), typeof(CashoutTransactionStateSavedEvent))
                        .From("transactions").On(defaultRoute)
                    .PublishingCommands(typeof(SendBitcoinCommand), typeof(BitcoinCashOutCommand))
                        .To("bitcoin").With(defaultPipeline)
                    .PublishingCommands(typeof(ChronoBankCashOutCommand))
                        .To("chronobank").With(defaultPipeline)
                    .PublishingCommands(typeof(ProcessEthereumCashoutCommand))
                        .To("ethereum").With(defaultPipeline)
                    .PublishingCommands(typeof(SolarCashOutCommand))
                        .To("solarcoin").With(defaultPipeline)
                    .PublishingCommands(typeof(BlockchainCashoutProcessor.Contract.Commands.StartCashoutCommand))
                        .To(BlockchainCashoutProcessor.Contract.BlockchainCashoutProcessorBoundedContext.Name).With(defaultPipeline),

                Register.Saga<ForwardWithdawalSaga>("forward-withdrawal-saga")
                    .ListeningEvents(typeof(CashoutTransactionStateSavedEvent))
                        .From("transactions").On(defaultRoute)
                    .PublishingCommands(typeof(SetLinkedCashInOperationCommand))
                        .To("forward-withdrawal").With(defaultPipeline),

                Register.Saga<TradeSaga>("trade-saga")
                    .ListeningEvents(typeof(TradeCreatedEvent), typeof(TransactionCreatedEvent))
                        .From("tx-handler").On(defaultRoute)
                    .ListeningEvents(typeof(EthTransactionRequestCreatedEvent))
                        .From("ethereum").On(defaultRoute)
                    .ListeningEvents(typeof(OffchainRequestCreatedEvent))
                        .From("offchain").On(defaultRoute)
                    .PublishingCommands(typeof(CreateTransactionCommand))
                        .To("tx-handler").With(defaultPipeline)
                    .PublishingCommands(typeof(EthGuaranteeTransferCommand), typeof(EthBuyCommand), typeof(EthCreateTransactionRequestCommand))
                        .To("ethereum").With(defaultPipeline)
                    .PublishingCommands(typeof(ReturnCommand), typeof(TransferFromHubCommand))
                        .To("offchain").With(defaultPipeline)
                    .PublishingCommands(typeof(OffchainNotifyCommand))
                        .To("notifications").With(defaultPipeline),

                Register.Saga<TransferSaga>("transfers-saga")
                    .ListeningEvents(typeof(TransferCreatedEvent))
                        .From("transfers").On(defaultRoute)
                    .PublishingCommands(typeof(EthTransferTrustedWalletCommand))
                        .To("transfers").With(defaultPipeline),

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
                        .To("offchain").With(defaultPipeline)
                    .PublishingCommands(typeof(SegwitTransferCommand))
                        .To("bitcoin").With(defaultPipeline)
                    .PublishingCommands(typeof(RegisterCashInOutOperationCommand))
                        .To("operations").With(defaultPipeline)
                    .PublishingCommands(typeof(SaveIssueTransactionStateCommand), typeof(SaveDestroyTransactionStateCommand), typeof(SaveCashoutTransactionStateCommand))
                        .To("transactions").With(defaultPipeline)
                    .PublishingCommands(typeof(CreateTradeCommand))
                        .To("tx-handler").With(defaultPipeline)
                    .PublishingCommands(typeof(CreateTransferCommand))
                        .To("transfers").With(defaultPipeline)
                );
            })
            .As<ICqrsEngine>().SingleInstance();
        }
    }
}
