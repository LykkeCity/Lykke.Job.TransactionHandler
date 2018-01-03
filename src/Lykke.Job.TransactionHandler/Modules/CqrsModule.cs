using System.Collections.Generic;
using Autofac;
using Common.Log;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
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

            builder.Register(context => new AutofacDependencyResolver(context)).As<IDependencyResolver>().SingleInstance();

            var rabbitMqSettings = new RabbitMQ.Client.ConnectionFactory { Uri = _settings.RabbitMq.ConnectionString };
            var messagingEngine = new MessagingEngine(_log,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"RabbitMq", new TransportInfo(rabbitMqSettings.Endpoint.ToString(), rabbitMqSettings.UserName, rabbitMqSettings.Password, "None", "RabbitMq")}
                }),
                new RabbitMqTransportFactory());

            builder.RegisterType<CashoutSaga>();

            builder.RegisterType<CashInOutCommandHandler>()
                .WithParameter(TypedParameter.From(_settings.Ethereum));
            builder.RegisterType<OffchainCommandHandler>();
            builder.RegisterType<HistoryProjection>();

            var defaultRetryDelay = _settings.TransactionHandlerJob.RetryDelayInMilliseconds;
            builder.Register(ctx =>
            {
                var txProjection = ctx.Resolve<HistoryProjection>();

                return new CqrsEngine(_log,
                    ctx.Resolve<IDependencyResolver>(),
                    messagingEngine,
                    new DefaultEndpointProvider(),
                    true,
                    Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("RabbitMq", "protobuf", environment: _settings.TransactionHandlerJob.Environment)),

                Register.BoundedContext("tx-handler")
                    .FailedCommandRetryDelay(defaultRetryDelay)
                    .ListeningCommands(typeof(CreateOffchainCashoutRequestCommand), typeof(IssueCommand), typeof(CashoutCommand), typeof(DestroyCommand), typeof(ManualUpdateCommand))
                        .On("tx-handler-commands")
                    //.PublishingEvents(typeof(TradeCreatedEvent), typeof(TransactionCreatedEvent))
                    //    .With("tx-handler-events")
                    //.WithCommandsHandler<CashInOutCommandHandler>(),
                    .WithCommandsHandlers(typeof(OffchainCommandHandler), typeof(CashInOutCommandHandler)),

                //Register.BoundedContext("transfers")
                //    .FailedCommandRetryDelay((long)TimeSpan.FromSeconds(5).TotalMilliseconds)
                //    .ListeningCommands(typeof(CreateTransferCommand))
                //        .On("transfers-commands")
                //    .PublishingEvents(typeof(TransferCreatedEvent))
                //        .With("transfers-events")
                //    .WithCommandsHandler<TransferCommandHandler>(),

                //Register.BoundedContext("history")
                //    .ListeningEvents(typeof(TradeCreatedEvent))
                //        .From("tx-handler").On("tx-handler-events")
                //    .WithProjection(txProjection, "tx-handler"),

                //Register.BoundedContext("ethereum")
                //    .ListeningCommands(typeof(EthCreateTransactionRequestCommand), typeof(EthGuaranteeTransferCommand), typeof(EthBuyCommand))
                //        .On("ethereum-commands")
                //    .PublishingEvents(typeof(EthTransactionRequestCreatedEvent), typeof(EthGuaranteeTransferCompletedEvent), typeof(EthTransferCompletedEvent))
                //        .With("ethereum-events")
                //    .WithCommandsHandler<EthereumCommandHandler>(),

                //Register.BoundedContext("bitcoin")
                //    .ListeningCommands(typeof(TransferFromHubCommand), typeof(ReturnCommand))
                //        .On("bitcoin-commands")
                //    .PublishingEvents(typeof(OffchainRequestCreatedEvent))
                //        .With("bitcoin-events")
                //    .WithCommandsHandler<BitcoinCommandHandler>(),

                //Register.BoundedContext("notifications")
                //    .ListeningCommands(typeof(OffchainNotifyCommand))
                //        .On("notifications-commands")
                //    .WithCommandsHandler<NotificationsCommandHandler>(),

                Register.Saga<CashoutSaga>("cash-in-out-saga"),
                //    .ListeningEvents(typeof(TradeCreatedEvent), typeof(TransactionCreatedEvent))
                //        .From("tx-handler").On("tx-handler-events")
                //    .ListeningEvents(typeof(EthTransactionRequestCreatedEvent))
                //        .From("ethereum").On("ethereum-events")
                //    .ListeningEvents(typeof(OffchainRequestCreatedEvent))
                //        .From("bitcoin").On("bitcoin-events")
                //    .PublishingCommands(typeof(CreateTransactionCommand))
                //        .To("tx-handler").With("tx-handler-commands")
                //    .PublishingCommands(typeof(EthGuaranteeTransferCommand), typeof(EthBuyCommand), typeof(EthCreateTransactionRequestCommand))
                //        .To("ethereum").With("ethereum-commands")
                //    .PublishingCommands(typeof(ReturnCommand), typeof(TransferFromHubCommand))
                //        .To("bitcoin").With("bitcoin-commands")
                //    .PublishingCommands(typeof(OffchainNotifyCommand))
                //        .To("notifications").With("notifications-commands"),

                Register.DefaultRouting
                    .PublishingCommands(typeof(CreateOffchainCashoutRequestCommand), typeof(IssueCommand), typeof(CashoutCommand), typeof(DestroyCommand), typeof(ManualUpdateCommand))
                        .To("tx-handler").With("tx-handler-commands")
                );
            })
            .As<ICqrsEngine>().SingleInstance();
        }
    }
}
