using System;
using System.Collections.Generic;
using System.Linq;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.CashOperations;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Service.Assets.Client.Custom;
using Lykke.Service.Assets.Client.Models;
using Newtonsoft.Json;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    public class TradeQueueItem
    {
        [JsonProperty("order")]
        public MarketOrder Order { get; set; }

        [JsonProperty("trades")]
        public List<TradeInfo> Trades { get; set; }

        public class MarketOrder : IMarketOrder
        {
            [JsonProperty("externalId")]
            public string Id { get; set; }

            [JsonProperty("id")]
            public string MatchingId { get; set; }

            [JsonProperty("assetPairId")]
            public string AssetPairId { get; set; }

            [JsonProperty("clientId")]
            public string ClientId { get; set; }

            [JsonProperty("volume")]
            public double Volume { get; set; }

            [JsonProperty("price")]
            public double? Price { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("createdAt")]
            public DateTime CreatedAt { get; set; }

            [JsonProperty("registered")]
            public DateTime Registered { get; set; }

            [JsonProperty("matchedAt")]
            public DateTime? MatchedAt { get; set; }

            [JsonProperty("straight")]
            public bool Straight { get; set; }

            [JsonProperty("reservedLimitVolume")]
            public double ReservedLimitVolume { get; set; }

            [JsonProperty("dustSize")]
            public double? DustSize { get; set; }

            double IOrderBase.Price
            {
                get { return Price.GetValueOrDefault(); }
                set { Price = value; }
            }
        }

        public class TradeInfo
        {
            [JsonProperty("price")]
            public double? Price { get; set; }

            [JsonProperty("limitOrderId")]
            public string LimitOrderId { get; set; }

            [JsonProperty("limitOrderExternalId")]
            public string LimitOrderExternalId { get; set; }

            [JsonProperty("timestamp")]
            public DateTime Timestamp { get; set; }

            [JsonProperty("marketClientId")]
            public string MarketClientId { get; set; }

            [JsonProperty("marketAsset")]
            public string MarketAsset { get; set; }

            [JsonProperty("marketVolume")]
            public double MarketVolume { get; set; }

            [JsonProperty("limitClientId")]
            public string LimitClientId { get; set; }

            [JsonProperty("limitVolume")]
            public double LimitVolume { get; set; }

            [JsonProperty("limitAsset")]
            public string LimitAsset { get; set; }
        }
    }

    public static class Ext
    {
        public static IClientTrade[] GetTradeRecords(this TradeQueueItem.TradeInfo trade, IMarketOrder marketOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsMarket,
            IWalletCredentials walletCredentialsLimit)
        {
            var result = new List<IClientTrade>();

            result.AddRange(CreateTradeRecordsForClient(trade, marketOrder, btcTransactionId, walletCredentialsMarket, walletCredentialsLimit, true));
            result.AddRange(CreateTradeRecordsForClient(trade, marketOrder, btcTransactionId, walletCredentialsMarket, walletCredentialsLimit, false));

            return result.ToArray();
        }

        private static IClientTrade[] CreateTradeRecordsForClientWithVolumes(TradeQueueItem.TradeInfo trade, IMarketOrder marketOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsMarket, IWalletCredentials walletCredentialsLimit,
            bool isMarketClient, double marketVolume, double limitVolume)
        {
            var clientId = isMarketClient ? walletCredentialsMarket?.ClientId : walletCredentialsLimit?.ClientId;

            if (!isMarketClient && string.IsNullOrWhiteSpace(clientId))
                return new IClientTrade[0];

            clientId = clientId ?? marketOrder.ClientId;

            var mutlisig = isMarketClient ? walletCredentialsMarket?.MultiSig : walletCredentialsLimit?.MultiSig;
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

            marketAssetRecord.Id = Utils.GenerateRecordId(marketAssetRecord.DateTime);
            limitAssetRecord.Id = Utils.GenerateRecordId(limitAssetRecord.DateTime);

            return new IClientTrade[] { marketAssetRecord, limitAssetRecord };
        }

        private static IClientTrade[] CreateTradeRecordsForClient(TradeQueueItem.TradeInfo trade, IMarketOrder marketOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsMarket, IWalletCredentials walletCredentialsLimit,
            bool isMarketClient)
        {
            return CreateTradeRecordsForClientWithVolumes(trade, marketOrder, btcTransactionId, walletCredentialsMarket,
                walletCredentialsLimit, isMarketClient, trade.MarketVolume, trade.LimitVolume);
        }

        private static ClientTrade CreateCommonPartForTradeRecord(TradeQueueItem.TradeInfo trade, IMarketOrder marketOrder,
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

        public static IClientTrade[] ToDomainOffchain(this TradeQueueItem item, IWalletCredentials walletCredentialsMarket, IWalletCredentials walletCredentialsLimit,
            IReadOnlyCollection<IAsset> assets)
        {
            var trade = item.Trades[0];

            var marketVolume = item.Trades.Sum(x => x.MarketVolume);
            var limitVolume = item.Trades.Sum(x => x.LimitVolume);

            var result = new List<IClientTrade>();

            result.AddRange(CreateTradeRecordsForClientWithVolumes(trade, item.Order, item.Order.Id, walletCredentialsMarket, walletCredentialsLimit, true, marketVolume, limitVolume));

            foreach (var clientTrade in result)
            {
                var isBtcAsset = IsBitcoinAsset(assets, clientTrade.AssetId);
                // if client guarantee transaction, then it is already settled
                if (clientTrade.ClientId == item.Order.ClientId && clientTrade.Amount < 0)
                    clientTrade.State = TransactionStates.SettledOffchain;
                else
                    clientTrade.State = isBtcAsset ? TransactionStates.InProcessOnchain : TransactionStates.InProcessOffchain;
            }

            return result.ToArray();
        }

        private static bool IsBitcoinAsset(IEnumerable<IAsset> assets, string assetId)
        {
            var asset = assets.FirstOrDefault(x => x.Id == assetId);

            if (asset == null)
                throw new ArgumentException("Unknown asset");

            return asset.Blockchain == Blockchain.Bitcoin;
        }
    }
}