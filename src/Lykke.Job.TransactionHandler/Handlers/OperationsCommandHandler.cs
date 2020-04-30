using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Operations.Client;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OperationsCommandHandler
    {
        private readonly ILog _log;
        private readonly ITransactionsRepository _transactionsRepository;
        private readonly ITransactionService _transactionService;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IFeeCalculationService _feeCalculationService;
        private readonly IOperationsClient _operationsClient;

        public OperationsCommandHandler(
            [NotNull] ILogFactory logFactory,
            [NotNull] ITransactionsRepository transactionsRepository,
            [NotNull] ITransactionService transactionService,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,
            [NotNull] IFeeCalculationService feeCalculationService,
            [NotNull] IOperationsClient operationsClient)
        {
            _log = logFactory.CreateLog(this) ?? throw new ArgumentNullException(nameof(logFactory));
            _transactionsRepository = transactionsRepository ?? throw new ArgumentNullException(nameof(transactionsRepository));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
            _feeCalculationService = feeCalculationService ?? throw new ArgumentNullException(nameof(feeCalculationService));
            _operationsClient = operationsClient ?? throw new ArgumentNullException(nameof(operationsClient));
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveCashoutOperationStateCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                await SaveState(command.Command, command.Context);

                eventPublisher.PublishEvent(new CashoutTransactionStateSavedEvent { Message = command.Message, Command = command.Command });

                return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(OperationsCommandHandler),  Command = nameof(Commands.SaveCashoutOperationStateCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveIssueOperationStateCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                await SaveState(command.Command, command.Context);

                eventPublisher.PublishEvent(new IssueTransactionStateSavedEvent { Message = command.Message, Command = command.Command });

                return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(OperationsCommandHandler),  Command = nameof(Commands.SaveIssueOperationStateCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveManualOperationStateCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                eventPublisher.PublishEvent(new ManualTransactionStateSavedEvent { Message = command.Message });
                return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(OperationsCommandHandler),  Command = nameof(Commands.SaveManualOperationStateCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }

        public async Task<CommandHandlingResult> Handle(Commands.SaveTransferOperationStateCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                var message = command.QueueMessage;
                var transactionId = message.Id;

                var transaction = await _transactionsRepository.FindByTransactionIdAsync(transactionId);
                if (transaction == null)
                {
                    _log.Error(nameof(Commands.SaveManualOperationStateCommand), new Exception($"unknown transaction {transactionId}"), context: command);
                    return CommandHandlingResult.Ok();
                }

                var amountNoFee = await _feeCalculationService.GetAmountNoFeeAsync(message);

                var context = await _transactionService.GetTransactionContext<TransferContextData>(transactionId) ??
                              TransferContextData.Create(
                                  message.FromClientId,
                                  new TransferContextData.TransferModel
                                  {
                                      ClientId = message.ToClientid
                                  },
                                  new TransferContextData.TransferModel
                                  {
                                      ClientId = message.FromClientId
                                  });

                context.Transfers[0].OperationId = Guid.NewGuid().ToString();
                context.Transfers[1].OperationId = Guid.NewGuid().ToString();

                var destWallet = await _walletCredentialsRepository.GetAsync(message.ToClientid);
                var sourceWallet = await _walletCredentialsRepository.GetAsync(message.FromClientId);

                var contextJson = context.ToJson();
                var cmd = new TransferCommand
                {
                    Amount = amountNoFee,
                    AssetId = message.AssetId,
                    Context = contextJson,
                    SourceAddress = sourceWallet?.MultiSig,
                    DestinationAddress = destWallet?.MultiSig,
                    TransactionId = Guid.Parse(transactionId)
                };

                await SaveState(cmd, context);

                eventPublisher.PublishEvent(new TransferOperationStateSavedEvent { TransactionId = transactionId, QueueMessage = message, AmountNoFee = (double) amountNoFee });

                return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(OperationsCommandHandler),  Command = nameof(Commands.SaveTransferOperationStateCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }

        }

        private async Task SaveState(BaseCommand command, BaseContextData context)
        {
            var transactionId = command.TransactionId.ToString();
            var requestData = command.ToJson();

            await _transactionsRepository.UpdateAsync(transactionId, requestData, null, "");

            ChaosKitty.Meow();

            await _transactionService.SetTransactionContext(transactionId, context);

            ChaosKitty.Meow();
        }

        public async Task<CommandHandlingResult> Handle(Commands.CompleteOperationCommand command, IEventPublisher eventPublisher)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                await _operationsClient.Complete(command.CommandId);
                return CommandHandlingResult.Ok();
            }
            finally
            {
                sw.Stop();
                _log.Info("Command execution time",
                    context: new { Handler = nameof(OperationsCommandHandler),  Command = nameof(Commands.CompleteOperationCommand),
                        Time = $"{sw.ElapsedMilliseconds} msec."
                    });
            }
        }
    }
}
