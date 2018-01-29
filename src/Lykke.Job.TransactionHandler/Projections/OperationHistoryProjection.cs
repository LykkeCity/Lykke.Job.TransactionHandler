using System;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Handlers;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class OperationHistoryProjection
    {        
        private readonly IClientCacheRepository _clientCacheRepository;
        private readonly ILog _log;
        private readonly ITradeOperationsRepositoryClient _clientTradesRepository;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly ITransferOperationsRepositoryClient _transferEventsRepositoryClient;
        private readonly Core.Services.BitCoin.ITransactionService _transactionService;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;

        public OperationHistoryProjection(
            [NotNull] ILog log,
            [NotNull] ITradeOperationsRepositoryClient clientTradesRepository,
            [NotNull] ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            [NotNull] ITransferOperationsRepositoryClient transferEventsRepositoryClient,
            [NotNull] Core.Services.BitCoin.ITransactionService transactionService,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,
            [NotNull] IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            IClientCacheRepository clientCacheRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clientTradesRepository = clientTradesRepository ?? throw new ArgumentNullException(nameof(clientTradesRepository));
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient ?? throw new ArgumentNullException(nameof(cashOperationsRepositoryClient));
            _transferEventsRepositoryClient = transferEventsRepositoryClient ?? throw new ArgumentNullException(nameof(transferEventsRepositoryClient));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository ?? throw new ArgumentNullException(nameof(ethereumTransactionRequestRepository));
        }

        public async Task Handle(TransferOperationStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(TransferOperationStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.QueueMessage;
            var transactionId = message.Id;
            var amount = evt.Amount;

            var context = await _transactionService.GetTransactionContext<TransferContextData>(transactionId);

            //Get eth request if it is ETH transfer
            var ethTxRequest = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(transactionId));

            //Get client wallets
            var destWallet = await _walletCredentialsRepository.GetAsync(message.ToClientid);
            var sourceWallet = await _walletCredentialsRepository.GetAsync(message.FromClientId);

            //Register transfer events
            var transferState = ethTxRequest == null
                ? TransactionStates.SettledOffchain
                : ethTxRequest.OperationType == OperationType.TransferBetweenTrusted
                    ? TransactionStates.SettledNoChain
                    : TransactionStates.SettledOnchain;

            await RegisterOperation(
                new TransferEvent
                {
                    Id = context.Transfers.Single(x => x.ClientId == message.ToClientid).OperationId,
                    ClientId = message.ToClientid,
                    DateTime = DateTime.UtcNow,
                    FromId = null,
                    AssetId = message.AssetId,
                    Amount = amount,
                    TransactionId = transactionId,
                    IsHidden = false,
                    AddressFrom = destWallet?.Address,
                    AddressTo = destWallet?.MultiSig,
                    Multisig = destWallet?.MultiSig,
                    IsSettled = false,
                    State = transferState
                });

            await RegisterOperation(
                new TransferEvent
                {
                    Id = context.Transfers.Single(x => x.ClientId == message.FromClientId).OperationId,
                    ClientId = message.FromClientId,
                    DateTime = DateTime.UtcNow,
                    FromId = null,
                    AssetId = message.AssetId,
                    Amount = -amount,
                    TransactionId = transactionId,
                    IsHidden = false,
                    AddressFrom = sourceWallet?.Address,
                    AddressTo = sourceWallet?.MultiSig,
                    Multisig = sourceWallet?.MultiSig,
                    IsSettled = false,
                    State = transferState
                });
        }

        private async Task RegisterOperation(TransferEvent operation)
        {
            var response = await _transferEventsRepositoryClient.RegisterAsync(operation);
            if (response.Id != operation.Id)
            {
                await _log.WriteWarningAsync(nameof(OperationHistoryProjection),
                    nameof(RegisterOperation), operation.ToJson(),
                    $"Unexpected response from Operations Service: {response.ToJson()}");
            }
        }

        public async Task Handle(TradeCreatedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(TradeCreatedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            if (evt.ClientTrades != null)
            {
                await _clientTradesRepository.SaveAsync(evt.ClientTrades);
            }
        }

        public async Task Handle(ManualTransactionStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(ManualTransactionStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.Message;
            var walletCredentials = await _walletCredentialsRepository.GetAsync(message.ClientId);

            var transactionId = message.Id;
            var context = await _transactionService.GetTransactionContext<CashOutContextData>(transactionId);
            var operation = new CashInOutOperation
            {
                Id = transactionId,
                ClientId = message.ClientId,
                Multisig = walletCredentials.MultiSig,
                AssetId = message.AssetId,
                Amount = message.Amount.ParseAnyDouble(),
                DateTime = DateTime.UtcNow,
                AddressFrom = walletCredentials.MultiSig,
                AddressTo = context.Address,
                TransactionId = transactionId,
                Type = CashOperationType.None,
                BlockChainHash = string.Empty,
                State = TransactionStates.SettledOffchain
            };

            await RegisterOperation(operation);
        }

        public async Task Handle(IssueTransactionStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(IssueTransactionStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.Message;
            var multisig = evt.Command.Multisig;
            var amount = evt.Command.Amount;
            var transactionId = evt.Command.TransactionId.ToString();
            var context = await _transactionService.GetTransactionContext<IssueContextData>(transactionId);
            var operation = new CashInOutOperation
            {
                Id = context.CashOperationId,
                ClientId = message.ClientId,
                Multisig = multisig,
                AssetId = message.AssetId,
                Amount = Math.Abs(amount),
                DateTime = DateTime.UtcNow,
                AddressTo = multisig,
                TransactionId = transactionId,
                State = TransactionStates.SettledOffchain
            };

            await RegisterOperation(operation);
        }

        public async Task Handle(CashoutTransactionStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(CashoutTransactionStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.Message;
            var walletCredentials = await _walletCredentialsRepository.GetAsync(message.ClientId);
            var amount = evt.Command.Amount;
            var transactionId = evt.Command.TransactionId.ToString();
            var context = await _transactionService.GetTransactionContext<CashOutContextData>(transactionId);
            var isForwardWithdawal = context.AddData?.ForwardWithdrawal != null;

            var operation = new CashInOutOperation
            {
                Id = context.CashOperationId,
                ClientId = message.ClientId,
                Multisig = walletCredentials.MultiSig,
                AssetId = message.AssetId,
                Amount = -Math.Abs(amount),
                DateTime = DateTime.UtcNow,
                AddressFrom = walletCredentials.MultiSig,
                AddressTo = context.Address,
                TransactionId = transactionId,
                Type = isForwardWithdawal ? CashOperationType.ForwardCashOut : CashOperationType.None,
                BlockChainHash = string.Empty,
                State = TransactionStates.SettledOffchain
            };

            await RegisterOperation(operation);
        }

        public async Task Handle(ForwardWithdawalLinkedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(ForwardWithdawalLinkedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.Message;
            var walletCredentials = await _walletCredentialsRepository.GetAsync(message.ClientId);
            var amount = message.Amount.ParseAnyDouble();
            var transactionId = message.Id;
            var context = await _transactionService.GetTransactionContext<CashOutContextData>(transactionId);

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(message.AssetId);
            var baseAsset = await _assetsServiceWithCache.TryGetAssetAsync(asset.ForwardBaseAsset);

            var operation = new CashInOutOperation
            {
                Id = context.AddData.ForwardWithdrawal.Id,
                ClientId = message.ClientId,
                Multisig = walletCredentials.MultiSig,
                AssetId = baseAsset.Id,
                Amount = Math.Abs(amount),
                DateTime = DateTime.UtcNow.AddDays(asset.ForwardFrozenDays),
                AddressFrom = walletCredentials.MultiSig,
                AddressTo = context.Address,
                TransactionId = transactionId,
                Type = CashOperationType.ForwardCashIn,
                State = TransactionStates.InProcessOffchain
            };

            await RegisterOperation(operation);
        }

        public async Task Handle(LimitOrderSavedEvent evt)
        {
            if (evt.IsTrustedClient)
                return;
            
            var activeLimitOrdersCount = evt.ActiveLimitOrders.Count();

            await _clientCacheRepository.UpdateLimitOrdersCount(evt.ClientId, activeLimitOrdersCount);

            _log.WriteInfo(nameof(OperationHistoryProjection), JsonConvert.SerializeObject(evt, Formatting.Indented), $"Client {evt.ClientId}. Limit orders cache updated: {activeLimitOrdersCount} active orders");
        }

        private async Task RegisterOperation(CashInOutOperation operation)
        {
            var operationId = await _cashOperationsRepositoryClient.RegisterAsync(operation);
            if (operationId != operation.Id)
            {
                await _log.WriteWarningAsync(nameof(OperationHistoryProjection),
                    nameof(RegisterOperation), operation.ToJson(),
                    $"Unexpected response from Operations Service: {operationId}");
            }
        }        
    }
}
