using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Queues;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Queues.Models;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class CashInOutMessageProcessor
    {
        private readonly ILog _log;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly ITransactionsRepository _transactionsRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly ITransactionService _transactionService;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly IBitcoinCashinRepository _bitcoinCashinTypeRepository;

        public CashInOutMessageProcessor(
            [NotNull] ILog log,
            [NotNull] ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            [NotNull] ITransactionsRepository transactionsRepository,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,
            [NotNull] IClientAccountClient clientAccountClient,
            [NotNull] ITransactionService transactionService,
            [NotNull] ICqrsEngine cqrsEngine,
            [NotNull] IBitcoinCashinRepository bitcoinCashinRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient ?? throw new ArgumentNullException(nameof(cashOperationsRepositoryClient));
            _transactionsRepository = transactionsRepository ?? throw new ArgumentNullException(nameof(transactionsRepository));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
            _clientAccountClient = clientAccountClient ?? throw new ArgumentNullException(nameof(clientAccountClient));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _cqrsEngine = cqrsEngine ?? throw new ArgumentNullException(nameof(cqrsEngine));
            _bitcoinCashinTypeRepository = bitcoinCashinRepository ?? throw new ArgumentNullException(nameof(bitcoinCashinRepository));
        }

        public async Task ProcessMessage(CashInOutQueueMessage message)
        {
            await _log.WriteInfoAsync(nameof(CashInOutMessageProcessor), nameof(ProcessMessage), message.ToJson());

            ChaosKitty.Meow();

            var transaction = await _transactionsRepository.FindByTransactionIdAsync(message.Id);
            if (transaction == null)
            {
                if (_cashOperationsRepositoryClient.GetAsync(message.ClientId, message.Id) == null)
                {
                    await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(CashInOutQueueMessage), message.ToJson(), "unknown transaction");
                    return;
                }

                await ProcessExternalCashin(message);
            }
            else
            {
                switch (transaction.CommandType)
                {
                    case BitCoinCommands.CashIn:
                    case BitCoinCommands.Issue:
                        await ProcessIssue(message);
                        break;
                    case BitCoinCommands.CashOut:
                        await ProcessCashOut(message);
                        break;
                    case BitCoinCommands.ManualUpdate:
                        ProcessManualUpdate(message);
                        break;
                    default:
                        await _log.WriteWarningAsync(nameof(CashInOutQueue), nameof(CashInOutQueueMessage), message.ToJson(), $"Unknown command type (value = [{transaction.CommandType}])");
                        break;
                }
            }
        }

        private async Task ProcessExternalCashin(CashInOutQueueMessage message)
        {
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(message.AssetId);

            if (asset.Blockchain != Blockchain.Bitcoin || asset.IsTrusted && asset.Id != LykkeConstants.BitcoinAssetId)
                return;

            if (asset.Id == LykkeConstants.BitcoinAssetId)
            {
                var cashinType = await _bitcoinCashinTypeRepository.GetAsync(message.Id);
                if (cashinType == null || !cashinType.IsSegwit)
                {
                    _cqrsEngine.SendCommand(new CreateOffchainCashoutRequestCommand
                    {
                        Id = message.Id,
                        ClientId = message.ClientId,
                        AssetId = message.AssetId,
                        Amount = (decimal)message.Amount.ParseAnyDouble()
                    }, BoundedContexts.TxHandler, BoundedContexts.Offchain);
                }
                else
                {
                    _cqrsEngine.SendCommand(new SegwitTransferCommand
                    {
                        Id = message.Id,
                        Address = cashinType.Address
                    }, BoundedContexts.TxHandler, BoundedContexts.Bitcoin);
                }
            }
        }

        private async Task ProcessIssue(CashInOutQueueMessage message)
        {
            var isClientTrusted = await _clientAccountClient.IsTrustedAsync(message.ClientId);
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(message.AssetId);
            if (!isClientTrusted.Value && !asset.IsTrusted)
            {
                await _log.WriteWarningAsync(nameof(CashInOutMessageProcessor), nameof(ProcessIssue), message.ToJson(), "Client and asset are not trusted.");
                return;
            }

            var walletCredentials = await _walletCredentialsRepository.GetAsync(message.ClientId);

            var amount = message.Amount.ParseAnyDecimal();
            var transactionId = message.Id;
            var context = await _transactionService.GetTransactionContext<IssueContextData>(transactionId);
            context.CashOperationId = transactionId;
            _cqrsEngine.SendCommand(new SaveIssueOperationStateCommand
            {
                Command = new IssueCommand
                {
                    TransactionId = Guid.Parse(transactionId),
                    Context = context.ToJson(),
                    Amount = Math.Abs(amount),
                    AssetId = message.AssetId,
                    Multisig = walletCredentials?.MultiSig
                },
                Context = context,
                Message = message
            }, BoundedContexts.TxHandler, BoundedContexts.Operations);
        }

        private void ProcessManualUpdate(CashInOutQueueMessage message)
        {
            _cqrsEngine.SendCommand(new SaveManualOperationStateCommand
            {
                Message = message
            }, BoundedContexts.TxHandler, BoundedContexts.Operations);
        }

        private async Task ProcessCashOut(CashInOutQueueMessage message)
        {
            var walletCredentials = await _walletCredentialsRepository.GetAsync(message.ClientId);
            
            var amount = message.Amount.ParseAnyDecimal();
            var transactionId = message.Id;
            var context = await _transactionService.GetTransactionContext<CashOutContextData>(transactionId);
            context.CashOperationId = transactionId;

            _cqrsEngine.SendCommand(new SaveCashoutOperationStateCommand
            {
                Command = new CashOutCommand
                {
                    Amount = Math.Abs(amount),
                    AssetId = message.AssetId,
                    Context = context.ToJson(),
                    SourceAddress = walletCredentials?.MultiSig,
                    DestinationAddress = context.Address,
                    TransactionId = Guid.Parse(transactionId)
                },
                Context = context,
                Message = message
            }, BoundedContexts.TxHandler, BoundedContexts.Operations);
        }
    }
}
