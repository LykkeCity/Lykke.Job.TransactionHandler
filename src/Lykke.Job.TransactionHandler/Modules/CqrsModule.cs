using System;
using System.Collections.Generic;
using Autofac;
using Common.Log;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Queues.Messaging;
using Lykke.Job.TransactionHandler.Services;
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
            var messagingEngine = new MessagingEngine(_log,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"RabbitMq", new TransportInfo($"amqp://{_settings.RabbitMq.ExternalHost}", _settings.RabbitMq.Username, _settings.RabbitMq.Password, "None", "RabbitMq")}
                }),
                new RabbitMqTransportFactory());

            builder.Register(context => new AutofacDependencyResolver(context)).As<IDependencyResolver>().SingleInstance();

            builder.Register(ctx =>
            {
                var txProjection = ctx.Resolve<HistoryProjection>();

                return new CqrsEngine(_log,
                    ctx.Resolve<IDependencyResolver>(),
                    messagingEngine,
                    new DefaultEndpointProvider(),
                    true,
                    Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("RabbitMq", "protobuf", environment: "dev"))
                        );

                //Register.BoundedContext("tx-handler")
                //    .FailedCommandRetryDelay((long)TimeSpan.FromSeconds(5).TotalMilliseconds)
                //    .ListeningCommands(typeof(CreateTradeCommand), typeof(CreateTransactionCommand))
                //        .On("tx-handler-commands")
                //    .PublishingEvents(typeof(TradeCreatedEvent), typeof(TransactionCreatedEvent))
                //        .With("tx-handler-events")
                //    .WithCommandsHandler<TradeCommandHandler>(),

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

                //Register.Saga<CashInOutSaga>("cash-in-out-saga")
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

                //Register.DefaultRouting
                //    .PublishingCommands(typeof(CreateTradeCommand), typeof(CreateTransactionCommand))
                //        .To("tx-handler").With("tx-handler-commands")
                //    .PublishingCommands(typeof(CreateTransferCommand))
                //        .To("transfers").With("transfers-commands")
                //    .PublishingCommands(typeof(EthCreateTransactionRequestCommand), typeof(EthGuaranteeTransferCommand), typeof(EthBuyCommand))
                //        .To("ethereum").With("ethereum-commands")
                //    .PublishingCommands(typeof(TransferFromHubCommand), typeof(ReturnCommand))
                //        .To("bitcoin").With("bitcoin-commands")
                //    .PublishingCommands(typeof(OffchainNotifyCommand))
                //        .To("notifications").With("notifications-commands"));
            }).As<ICqrsEngine>().SingleInstance();
        }
    }
}
