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
using System.Linq;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Services.Fee;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class CashInOutMessageProcessor
    {
        private readonly ILog _log;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionsRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;
        private readonly ICqrsEngine _cqrsEngine;
        private readonly IBitcoinCashinRepository _bitcoinCashinTypeRepository;
        private readonly IFeeLogService _feeLogService;

        public CashInOutMessageProcessor(
            [NotNull] ILog log,
            [NotNull] ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            [NotNull] IBitCoinTransactionsRepository bitcoinTransactionsRepository,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,
            [NotNull] IClientAccountClient clientAccountClient,
            [NotNull] IBitcoinTransactionService bitcoinTransactionService,
            [NotNull] ICqrsEngine cqrsEngine,
            [NotNull] IBitcoinCashinRepository bitcoinCashinRepository,
            [NotNull] IFeeLogService feeLogService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient ?? throw new ArgumentNullException(nameof(cashOperationsRepositoryClient));
            _bitcoinTransactionsRepository = bitcoinTransactionsRepository ?? throw new ArgumentNullException(nameof(bitcoinTransactionsRepository));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
            _clientAccountClient = clientAccountClient ?? throw new ArgumentNullException(nameof(clientAccountClient));
            _bitcoinTransactionService = bitcoinTransactionService ?? throw new ArgumentNullException(nameof(bitcoinTransactionService));
            _cqrsEngine = cqrsEngine ?? throw new ArgumentNullException(nameof(cqrsEngine));
            _bitcoinCashinTypeRepository = bitcoinCashinRepository ?? throw new ArgumentNullException(nameof(bitcoinCashinRepository));
            _feeLogService = feeLogService ?? throw new ArgumentNullException(nameof(feeLogService));
        }

        public async Task ProcessMessage(CashInOutQueueMessage message)
        {
            await _feeLogService.WriteFeeInfo(message);

            ChaosKitty.Meow();

            var transaction = await _bitcoinTransactionsRepository.FindByTransactionIdAsync(message.Id);
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
                        await ProcessIssue(transaction, message);
                        break;
                    case BitCoinCommands.CashOut:
                        await ProcessCashOut(transaction, message);
                        break;
                    case BitCoinCommands.Destroy:
                        await ProcessDestroy(transaction, message);
                        break;
                    case BitCoinCommands.ManualUpdate:
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
                    }, "tx-handler", "offchain");
                }
                else
                {
                    _cqrsEngine.SendCommand(new SegwitTransferCommand
                    {
                        Id = message.Id,
                        Address = cashinType.Address
                    }, "tx-handler", "bitcoin");
                }
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
            context.CashOperationId = transaction.TransactionId;
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
            context.CashOperationId = transaction.TransactionId;
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
            
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(message.AssetId);
            var feeAmount = (message.FeeTransfers?.FirstOrDefault()?.Volume ?? 0.0).TruncateDecimalPlaces(asset.Accuracy, true);
            var amount = message.Amount.ParseAnyDouble() - feeAmount;

            var context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transaction.TransactionId);

            context.CashOperationId = transaction.TransactionId;

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
