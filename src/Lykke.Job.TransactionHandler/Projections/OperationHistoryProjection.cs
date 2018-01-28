﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
using Lykke.Job.TransactionHandler.Handlers;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Projections
{
    public class OperationHistoryProjection
    {        
        private readonly IClientCacheRepository _clientCacheRepository;
        private readonly ILog _log;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly Core.Services.BitCoin.IBitcoinTransactionService _bitcoinTransactionService;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;

        public OperationHistoryProjection(
            [NotNull] ILog log,
            [NotNull] ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            [NotNull] Core.Services.BitCoin.IBitcoinTransactionService bitcoinTransactionService,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,            
            IClientCacheRepository clientCacheRepository)
        {            
            _clientCacheRepository = clientCacheRepository;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient ?? throw new ArgumentNullException(nameof(cashOperationsRepositoryClient));
            _bitcoinTransactionService = bitcoinTransactionService ?? throw new ArgumentNullException(nameof(bitcoinTransactionService));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
        }

        public async Task Handle(IssueTransactionStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(IssueTransactionStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.Message;
            var multisig = evt.Command.Multisig;
            var amount = evt.Command.Amount;
            var transactionId = evt.Command.TransactionId.ToString();
            var context = await _bitcoinTransactionService.GetTransactionContext<IssueContextData>(transactionId);
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

        public async Task Handle(DestroyTransactionStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(DestroyTransactionStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.Message;
            var amount = evt.Command.Amount;
            var transactionId = evt.Command.TransactionId.ToString();
            var context = await _bitcoinTransactionService.GetTransactionContext<UncolorContextData>(transactionId);
            var operation = new CashInOutOperation
            {
                Id = context.CashOperationId,
                ClientId = message.ClientId,
                Multisig = context.AddressFrom,
                AssetId = message.AssetId,
                Amount = -Math.Abs(amount),
                DateTime = DateTime.UtcNow,
                AddressFrom = context.AddressFrom,
                AddressTo = context.AddressTo,
                TransactionId = transactionId
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
            var context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transactionId);
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
            var context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transactionId);

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
                await _log.WriteWarningAsync(nameof(OperationsCommandHandler),
                    nameof(RegisterOperation), operation.ToJson(),
                    $"Unexpected response from Operations Service: {operationId}");
            }
        }        
    }
}
