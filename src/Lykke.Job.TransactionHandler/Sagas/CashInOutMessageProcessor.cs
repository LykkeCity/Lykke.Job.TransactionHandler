using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Clients.Core.Clients;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Queues;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class CashInOutMessageProcessor
    {
        private readonly ILog _log;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionsRepository;
        private readonly IClientSettingsRepository _clientSettingsRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly ICqrsEngine _cqrsEngine;

        public CashInOutMessageProcessor(
            [NotNull] ILog log,
            [NotNull] ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            [NotNull] IBitCoinTransactionsRepository bitcoinTransactionsRepository,
            [NotNull] IClientSettingsRepository clientSettingsRepository,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,
            [NotNull] IClientAccountClient clientAccountClient,
            [NotNull] IBitcoinTransactionService bitcoinTransactionService,
            [NotNull] ICqrsEngine cqrsEngine)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient ?? throw new ArgumentNullException(nameof(cashOperationsRepositoryClient));
            _bitcoinTransactionsRepository = bitcoinTransactionsRepository ?? throw new ArgumentNullException(nameof(bitcoinTransactionsRepository));
            _clientSettingsRepository = clientSettingsRepository ?? throw new ArgumentNullException(nameof(clientSettingsRepository));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
            _clientAccountClient = clientAccountClient ?? throw new ArgumentNullException(nameof(clientAccountClient));
            _bitcoinTransactionService = bitcoinTransactionService ?? throw new ArgumentNullException(nameof(bitcoinTransactionService));
            _cqrsEngine = cqrsEngine ?? throw new ArgumentNullException(nameof(cqrsEngine));
        }

        public async Task ProcessMessage(CashInOutQueueMessage message)
        {
            await _log.WriteInfoAsync(nameof(CashInOutMessageProcessor), nameof(CashInOutQueueMessage), message.ToJson(), "");

            ChaosKitty.Meow();

            var transaction = await _bitcoinTransactionsRepository.FindByTransactionIdAsync(message.Id);
            if (transaction == null) // swift?
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
                    case BitCoinCommands.Issue: // Roman?
                        await ProcessIssue(transaction, message);
                        break;
                    case BitCoinCommands.CashOut: // withdraw (trusted -> external / trading -> private)
                        await ProcessCashOut(transaction, message);
                        break;
                    case BitCoinCommands.Destroy:
                        await ProcessDestroy(transaction, message);
                        break;
                    case BitCoinCommands.ManualUpdate: // BO "+"
                        await ProcessManualUpdate(transaction, message);
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

            if (!await _clientSettingsRepository.IsOffchainClient(message.ClientId) || asset.Blockchain != Blockchain.Bitcoin ||
                asset.IsTrusted && asset.Id != LykkeConstants.BitcoinAssetId)
                return;

            if (asset.Id == LykkeConstants.BitcoinAssetId)
            {
                _cqrsEngine.SendCommand(new CreateOffchainCashoutRequestCommand
                {
                    Id = Guid.NewGuid().ToString(),
                    ClientId = message.ClientId,
                    AssetId = message.AssetId,
                    Amount = (decimal)message.Amount.ParseAnyDouble()
                }, "tx-handler", "offchain");
            }
        }

        private async Task ProcessIssue(IBitcoinTransaction transaction, CashInOutQueueMessage message)
        {
            var isClientTrusted = await _clientAccountClient.IsTrustedAsync(message.ClientId);
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(message.AssetId);
            if (!isClientTrusted.Value && !asset.IsTrusted)
            {
                await _log.WriteWarningAsync(nameof(CashInOutMessageProcessor), nameof(ProcessIssue), message.ToJson(), "Client and asset are not trusted.");
                return;
            }

            var walletCredentials = await _walletCredentialsRepository.GetAsync(message.ClientId);

            var amount = message.Amount.ParseAnyDouble();
            var context = await _bitcoinTransactionService.GetTransactionContext<IssueContextData>(transaction.TransactionId);
            context.CashOperationId = Guid.NewGuid().ToString();
            _cqrsEngine.SendCommand(new SaveIssueTransactionStateCommand
            {
                Command = new IssueCommand
                {
                    TransactionId = Guid.Parse(transaction.TransactionId),
                    Context = context.ToJson(),
                    Amount = Math.Abs(amount),
                    AssetId = message.AssetId,
                    Multisig = walletCredentials.MultiSig
                },
                Context = context,
                Message = message
            }, "tx-handler", "transactions");
        }

        private async Task ProcessDestroy(IBitcoinTransaction transaction, CashInOutQueueMessage message)
        {
            var amount = message.Amount.ParseAnyDouble();
            var context = await _bitcoinTransactionService.GetTransactionContext<UncolorContextData>(transaction.TransactionId);
            context.CashOperationId = Guid.NewGuid().ToString();
            _cqrsEngine.SendCommand(new SaveDestroyTransactionStateCommand
            {
                Command = new DestroyCommand
                {
                    TransactionId = Guid.Parse(transaction.TransactionId),
                    Context = context.ToJson(),
                    Amount = Math.Abs(amount),
                    AssetId = message.AssetId,
                    Address = context.AddressFrom
                },
                Context = context,
                Message = message
            }, "tx-handler", "transactions");
        }

        private async Task ProcessManualUpdate(IBitcoinTransaction transaction, CashInOutQueueMessage message)
        {
            _cqrsEngine.SendCommand(new RegisterCashInOutOperationCommand
            {
                Message = message
            }, "tx-handler", "operations");
        }

        private async Task ProcessCashOut(IBitcoinTransaction transaction, CashInOutQueueMessage message)
        {
            var walletCredentials = await _walletCredentialsRepository.GetAsync(message.ClientId);
            var amount = message.Amount.ParseAnyDouble();
            var context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transaction.TransactionId);
            
            context.CashOperationId = Guid.NewGuid().ToString();
            _cqrsEngine.SendCommand(new SaveCashoutTransactionStateCommand
            {
                Command = new CashOutCommand
                {
                    Amount = Math.Abs(amount),
                    AssetId = message.AssetId,
                    Context = context.ToJson(),
                    SourceAddress = walletCredentials.MultiSig,
                    DestinationAddress = context.Address,
                    TransactionId = Guid.Parse(transaction.TransactionId)
                },
                Context = context,
                Message = message
            }, "tx-handler", "transactions");
        }
    }
}
