using System;
using System.Collections.Generic;
using System.Linq;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.OperationsRepository.AutorestClient.Models;

namespace Lykke.Job.TransactionHandler.Services
{
    public static class ConvertExtensions
    {
        public static ClientTrade[] ToDomainOffchain(this LimitQueueItem.LimitOrderWithTrades item,
            string btcTransactionId, IWalletCredentials walletCredentialsLimitA,
            IWalletCredentials walletCredentialsLimitB)
        {
            var trade = item.Trades[0];

            var limitVolume = item.Trades.Sum(x => x.Volume);
            var oppositeLimitVolume = item.Trades.Sum(x => x.OppositeVolume);

            var result = new List<ClientTrade>();

            result.AddRange(CreateTradeRecordForClientWithVolumes(trade, item.Order, btcTransactionId,
                walletCredentialsLimitA, walletCredentialsLimitB, limitVolume, oppositeLimitVolume));

            return result.ToArray();
        }

        private static ClientTrade[] CreateTradeRecordForClientWithVolumes(LimitQueueItem.LimitTradeInfo trade,
            ILimitOrder limitOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsLimitA,
            IWalletCredentials walletCredentialsLimitB, double limitVolume, double oppositeLimitVolume)
        {
            var clientId = walletCredentialsLimitA?.ClientId ?? limitOrder.ClientId;

            var mutlisig = walletCredentialsLimitA?.MultiSig;
            var fromMultisig = walletCredentialsLimitB?.MultiSig;

            var depositAssetRecord = CreateCommonPartForTradeRecord(trade, limitOrder, btcTransactionId);
            var withdrawAssetRecord = CreateCommonPartForTradeRecord(trade, limitOrder, btcTransactionId);

            depositAssetRecord.ClientId = withdrawAssetRecord.ClientId = clientId;
            depositAssetRecord.AddressFrom = withdrawAssetRecord.AddressFrom = fromMultisig;
            depositAssetRecord.AddressTo = withdrawAssetRecord.AddressTo = mutlisig;
            depositAssetRecord.Multisig = withdrawAssetRecord.Multisig = mutlisig;

            depositAssetRecord.Amount = oppositeLimitVolume;
            depositAssetRecord.AssetId = trade.OppositeAsset;

            withdrawAssetRecord.Amount = -1 * limitVolume;
            withdrawAssetRecord.AssetId = trade.Asset;

            depositAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(depositAssetRecord.DateTime);
            withdrawAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(withdrawAssetRecord.DateTime);

            return new[] {depositAssetRecord, withdrawAssetRecord};
        }

        private static ClientTrade CreateCommonPartForTradeRecord(LimitQueueItem.LimitTradeInfo trade,
            ILimitOrder limitOrder,
            string btcTransactionId)
        {
            return new ClientTrade
            {
                DateTime = trade.Timestamp,
                Price = trade.Price,
                LimitOrderId = limitOrder.Id,
                OppositeLimitOrderId = trade.OppositeOrderExternalId,
                TransactionId = btcTransactionId,
                IsLimitOrderResult = true
            };
        }

        public static ClientTrade[] GetTradeRecords(this TradeQueueItem.TradeInfo trade, IMarketOrder marketOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsMarket,
            IWalletCredentials walletCredentialsLimit)
        {
            var result = new List<ClientTrade>();

            result.AddRange(CreateTradeRecordsForClient(trade, marketOrder, btcTransactionId, walletCredentialsMarket,
                walletCredentialsLimit, true));
            result.AddRange(CreateTradeRecordsForClient(trade, marketOrder, btcTransactionId, walletCredentialsMarket,
                walletCredentialsLimit, false));

            return result.ToArray();
        }

        private static ClientTrade[] CreateTradeRecordsForClientWithVolumes(TradeQueueItem.TradeInfo trade,
            IMarketOrder marketOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsMarket,
            IWalletCredentials walletCredentialsLimit,
            bool isMarketClient, double marketVolume, double limitVolume)
        {
            var clientId = isMarketClient ? walletCredentialsMarket?.ClientId : walletCredentialsLimit?.ClientId;

            if (!isMarketClient && string.IsNullOrWhiteSpace(clientId))
                return new ClientTrade[0];

            clientId = clientId ?? marketOrder.ClientId;

            var mutlisig = isMarketClient ? walletCredentialsMarket?.MultiSig : walletCredentialsLimit.MultiSig;
            var fromMultisig = isMarketClient ? walletCredentialsLimit?.MultiSig : walletCredentialsMarket?.MultiSig;

            var marketAssetRecord = CreateCommonPartForTradeRecord(trade, marketOrder, btcTransactionId);
            var limitAssetRecord = CreateCommonPartForTradeRecord(trade, marketOrder, btcTransactionId);

            marketAssetRecord.ClientId = limitAssetRecord.ClientId = clientId;
            marketAssetRecord.AddressFrom = limitAssetRecord.AddressFrom = fromMultisig;
            marketAssetRecord.AddressTo = limitAssetRecord.AddressTo = mutlisig;
            marketAssetRecord.Multisig = limitAssetRecord.Multisig = mutlisig;

            marketAssetRecord.Amount = marketVolume * (isMarketClient ? -1 : 1);
            marketAssetRecord.AssetId = trade.MarketAsset;

            limitAssetRecord.Amount = limitVolume * (isMarketClient ? 1 : -1);
            limitAssetRecord.AssetId = trade.LimitAsset;

            marketAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(marketAssetRecord.DateTime);
            limitAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(limitAssetRecord.DateTime);

            return new[] {marketAssetRecord, limitAssetRecord};
        }

        private static ClientTrade[] CreateTradeRecordsForClient(TradeQueueItem.TradeInfo trade,
            IMarketOrder marketOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsMarket,
            IWalletCredentials walletCredentialsLimit,
            bool isMarketClient)
        {
            return CreateTradeRecordsForClientWithVolumes(trade, marketOrder, btcTransactionId, walletCredentialsMarket,
                walletCredentialsLimit, isMarketClient, trade.MarketVolume, trade.LimitVolume);
        }

        private static ClientTrade CreateCommonPartForTradeRecord(TradeQueueItem.TradeInfo trade,
            IMarketOrder marketOrder,
            string btcTransactionId)
        {
            return new ClientTrade
            {
                DateTime = trade.Timestamp,
                Price = trade.Price.GetValueOrDefault(),
                LimitOrderId = trade.LimitOrderExternalId,
                MarketOrderId = marketOrder.Id,
                TransactionId = btcTransactionId
            };
        }

        public static ClientTrade[] ToDomainOffchain(this TradeQueueItem item,
            IWalletCredentials walletCredentialsMarket, IWalletCredentials walletCredentialsLimit,
            IReadOnlyCollection<Asset> assets)
        {
            var trade = item.Trades[0];

            var marketVolume = item.Trades.Sum(x => x.MarketVolume);
            var limitVolume = item.Trades.Sum(x => x.LimitVolume);

            var result = new List<ClientTrade>();

            result.AddRange(CreateTradeRecordsForClientWithVolumes(trade, item.Order, item.Order.Id,
                walletCredentialsMarket, walletCredentialsLimit, true, marketVolume, limitVolume));

            foreach (var clientTrade in result)
            {
                var asset = assets.FirstOrDefault(x => x.Id == clientTrade.AssetId);

                if (asset == null)
                    throw new ArgumentException("Unknown asset");

                // if client guarantee transaction or trusted asset, then it is already settled
                if (clientTrade.ClientId == item.Order.ClientId && clientTrade.Amount < 0 || asset.IsTrusted)
                    clientTrade.State = TransactionStates.SettledOffchain;
                else
                    clientTrade.State = TransactionStates.InProcessOffchain;
            }

            return result.ToArray();
        }
    }
}