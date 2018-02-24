﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.Fee;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Events.LimitOrders;
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
        private readonly ILimitTradeEventsRepositoryClient _limitTradeEventsRepositoryClient;
        private readonly ILog _log;
        private readonly ITradeOperationsRepositoryClient _clientTradesRepository;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly ITransferOperationsRepositoryClient _transferEventsRepositoryClient;
        private readonly Core.Services.BitCoin.ITransactionService _transactionService;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IFeeLogService _feeLogService;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;

        public OperationHistoryProjection(
            [NotNull] ILog log,
            [NotNull] ITradeOperationsRepositoryClient clientTradesRepository,
            [NotNull] ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            [NotNull] ITransferOperationsRepositoryClient transferEventsRepositoryClient,
            [NotNull] Core.Services.BitCoin.ITransactionService transactionService,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,
            [NotNull] IFeeLogService feeLogService,
            [NotNull] ILimitTradeEventsRepositoryClient limitTradeEventsRepositoryClient,
            [NotNull] IEthereumTransactionRequestRepository ethereumTransactionRequestRepository)
        {
            _limitTradeEventsRepositoryClient = limitTradeEventsRepositoryClient;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clientTradesRepository = clientTradesRepository ?? throw new ArgumentNullException(nameof(clientTradesRepository));
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient ?? throw new ArgumentNullException(nameof(cashOperationsRepositoryClient));
            _transferEventsRepositoryClient = transferEventsRepositoryClient ?? throw new ArgumentNullException(nameof(transferEventsRepositoryClient));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
            _feeLogService = feeLogService ?? throw new ArgumentNullException(nameof(feeLogService));
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository ?? throw new ArgumentNullException(nameof(ethereumTransactionRequestRepository));
        }


        public async Task Handle(TransferOperationStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(TransferOperationStateSavedEvent), evt.ToJson(), "");

            var message = evt.QueueMessage;
            var transactionId = message.Id;
            var amountNoFee = evt.AmountNoFee;

            var context = await _transactionService.GetTransactionContext<TransferContextData>(transactionId);

            //Get eth request if it is ETH transfer
            var ethTxRequest = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(transactionId));

            //Get client wallets
            var wallets = (await _walletCredentialsRepository.GetWalletsAsync(new[] { message.ToClientid, message.FromClientId })).ToList();
            var destWallet = wallets.FirstOrDefault(x => x.ClientId == message.ToClientid);
            var sourceWallet = wallets.FirstOrDefault(x => x.ClientId == message.FromClientId);

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
                    Amount = amountNoFee,
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
                    Amount = -amountNoFee,
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

            ChaosKitty.Meow();
        }

        public async Task Handle(TradeCreatedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(TradeCreatedEvent), evt.ToJson(), "");

            if (evt.ClientTrades != null)
            {
                await _clientTradesRepository.SaveAsync(evt.ClientTrades);
            }

            ChaosKitty.Meow();
        }

        public async Task Handle(ManualTransactionStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(ManualTransactionStateSavedEvent), evt.ToJson(), "");

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

            operation.AddFeeDataToOperation(message);

            await RegisterOperation(operation);
        }

        public async Task Handle(IssueTransactionStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(IssueTransactionStateSavedEvent), evt.ToJson(), "");

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

            operation.AddFeeDataToOperation(message);

            await RegisterOperation(operation);
        }

        public async Task Handle(CashoutTransactionStateSavedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(CashoutTransactionStateSavedEvent), evt.ToJson(), "");

            var message = evt.Message;
            var walletCredentials = await _walletCredentialsRepository.GetAsync(message.ClientId);
            var amount = message.Amount.ParseAnyDouble();
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

            operation.AddFeeDataToOperation(message);

            await RegisterOperation(operation);
        }

        public async Task Handle(ForwardWithdawalLinkedEvent evt)
        {
            await _log.WriteInfoAsync(nameof(OperationHistoryProjection), nameof(ForwardWithdawalLinkedEvent), evt.ToJson(), "");

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

            operation.AddFeeDataToOperation(message);

            await RegisterOperation(operation);
        }

        public async Task Handle(LimitOrderExecutedEvent evt)
        {
            var assetPairId = evt.LimitOrder.Order.AssetPairId;
            // Save trades
            if (evt.Trades != null && evt.Trades.Any())
            {
                var clientTrades = evt.Trades
                    .Select(t => new ClientTrade
                    {
                        Id = t.Id,
                        ClientId = t.ClientId,
                        AssetId = t.AssetId,
                        AssetPairId = assetPairId,
                        Amount = t.Amount,
                        DateTime = t.DateTime,
                        Price = t.Price,
                        LimitOrderId = t.LimitOrderId,
                        OppositeLimitOrderId = t.OppositeLimitOrderId,
                        TransactionId = t.TransactionId,
                        IsLimitOrderResult = t.IsLimitOrderResult,
                        State = t.State,
                        FeeSize = t.FeeSize,
                        FeeType = t.FeeType
                    }).ToArray();

                await _clientTradesRepository.SaveAsync(clientTrades);

                _log.WriteInfo(nameof(OperationHistoryProjection), JsonConvert.SerializeObject(clientTrades, Formatting.Indented), $"Client {evt.LimitOrder.Order.ClientId}. Limit trade {evt.LimitOrder.Order.Id}. Client trades saved");
            }
            else
            {
                _log.WriteInfo(nameof(OperationHistoryProjection), null, $"Client {evt.LimitOrder.Order.ClientId}. Limit order {evt.LimitOrder.Order.Id}. Client trades are empty");
            }

            // Save fee logs
            if (evt.LimitOrder.Trades != null && evt.LimitOrder.Trades.Any())
            {
                await _feeLogService.WriteFeeInfoAsync(evt.LimitOrder);
            }
            else
            {
                _log.WriteInfo(nameof(OperationHistoryProjection), null, $"Client {evt.LimitOrder.Order.ClientId}. Limit order {evt.LimitOrder.Order.Id}. Fee logs are empty, there are no trades.");
            }

            if (evt.IsTrustedClient)
                return;

            // Save context
            var contextData = await _transactionService.GetTransactionContext<SwapOffchainContextData>(evt.LimitOrder.Order.Id) ?? new SwapOffchainContextData();

            var aggregated = evt.Aggregated ?? new List<Handlers.AggregatedTransfer>();

            foreach (var operation in aggregated.Where(x => x.ClientId == evt.LimitOrder.Order.ClientId))
            {
                var trade = evt.Trades.FirstOrDefault(x => x.ClientId == operation.ClientId && x.AssetId == operation.AssetId && Math.Abs(x.Amount - (double)operation.Amount) < 0.00000001);

                contextData.Operations.Add(new SwapOffchainContextData.Operation()
                {
                    TransactionId = operation.TransferId,
                    Amount = operation.Amount,
                    ClientId = operation.ClientId,
                    AssetId = operation.AssetId,
                    ClientTradeId = trade?.Id
                });
            }

            await _transactionService.CreateOrUpdateAsync(evt.LimitOrder.Order.Id);
            await _transactionService.SetTransactionContext(evt.LimitOrder.Order.Id, contextData);

            _log.WriteInfo(nameof(OperationHistoryProjection), JsonConvert.SerializeObject(contextData, Formatting.Indented), $"Client {evt.LimitOrder.Order.ClientId}. Limit order {evt.LimitOrder.Order.Id}. Context updated.");

            // Save limit trade events
            var status = (OrderStatus)Enum.Parse(typeof(OrderStatus), evt.LimitOrder.Order.Status);

            switch (status)
            {
                case OrderStatus.InOrderBook:
                case OrderStatus.Cancelled:
                    await CreateEvent(evt.LimitOrder, status);
                    break;
                case OrderStatus.Processing:
                case OrderStatus.Matched:
                    if (!evt.HasPrevOrderState)
                        await CreateEvent(evt.LimitOrder, OrderStatus.InOrderBook);
                    break;
                case OrderStatus.Dust:
                case OrderStatus.NoLiquidity:
                case OrderStatus.NotEnoughFunds:
                case OrderStatus.ReservedVolumeGreaterThanBalance:
                case OrderStatus.UnknownAsset:
                case OrderStatus.LeadToNegativeSpread:
                case OrderStatus.TooSmallVolume:
                case OrderStatus.Runtime:
                    _log.WriteInfo(nameof(OperationHistoryProjection), JsonConvert.SerializeObject(evt.LimitOrder, Formatting.Indented), $"Client {evt.LimitOrder.Order.ClientId}. Order {evt.LimitOrder.Order.Id}: Rejected");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(OrderStatus));
            }
        }

        private async Task CreateEvent(LimitQueueItem.LimitOrderWithTrades limitOrderWithTrades, OrderStatus status)
        {
            var order = limitOrderWithTrades.Order;
            var type = order.Volume > 0 ? OrderType.Buy : OrderType.Sell;
            var assetPair = await _assetsServiceWithCache.TryGetAssetPairAsync(order.AssetPairId);
            var date = status == OrderStatus.InOrderBook ? limitOrderWithTrades.Order.CreatedAt : DateTime.UtcNow;

            var insertRequest = new LimitTradeEventInsertRequest
            {
                Volume = order.Volume,
                Type = type,
                OrderId = order.Id,
                Status = status,
                AssetId = assetPair?.BaseAssetId,
                ClientId = order.ClientId,
                Price = order.Price,
                AssetPair = order.AssetPairId,
                DateTime = date
            };

            await _limitTradeEventsRepositoryClient.CreateAsync(insertRequest);

            _log.WriteInfo(nameof(OperationHistoryProjection), JsonConvert.SerializeObject(insertRequest, Formatting.Indented), $"Client {order.ClientId}. Limit trade {order.Id}. State has changed -> {status}");
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

            ChaosKitty.Meow();
        }
    }
}
