using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Domain.Exchange;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Newtonsoft.Json;
using ProtoBuf;
using TransactionStates = Lykke.Service.OperationsRepository.AutorestClient.Models.TransactionStates;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    [ProtoContract]
    public class TradeQueueItem
    {
        [ProtoMember(1)]
        [JsonProperty("order")]
        public MarketOrder Order { get; set; }

        [ProtoMember(2)]
        [JsonProperty("trades")]
        public List<TradeInfo> Trades { get; set; }

        [ProtoContract]
        public class MarketOrder : IMarketOrder
        {
            [ProtoMember(1)]
            [JsonProperty("externalId")]
            public string Id { get; set; }

            [ProtoMember(2)]
            [JsonProperty("id")]
            public string MatchingId { get; set; }

            [ProtoMember(3)]
            [JsonProperty("assetPairId")]
            public string AssetPairId { get; set; }

            [ProtoMember(4)]
            [JsonProperty("clientId")]
            public string ClientId { get; set; }

            [ProtoMember(5)]
            [JsonProperty("volume")]
            public double Volume { get; set; }

            [ProtoMember(6)]
            [JsonProperty("price")]
            public double? Price { get; set; }

            [ProtoMember(7)]
            [JsonProperty("status")]
            public string Status { get; set; }

            [ProtoMember(8)]
            [JsonProperty("createdAt")]
            public DateTime CreatedAt { get; set; }

            [ProtoMember(9)]
            [JsonProperty("registered")]
            public DateTime Registered { get; set; }

            [ProtoMember(10)]
            [JsonProperty("matchedAt")]
            public DateTime? MatchedAt { get; set; }

            [ProtoMember(11)]
            [JsonProperty("straight")]
            public bool Straight { get; set; }

            [ProtoMember(12)]
            [JsonProperty("reservedLimitVolume")]
            public double ReservedLimitVolume { get; set; }

            [ProtoMember(13)]
            [JsonProperty("dustSize")]
            public double? DustSize { get; set; }

            [ProtoMember(14)]
            double IOrderBase.Price
            {
                get { return Price.GetValueOrDefault(); }
                set { Price = value; }
            }
        }

        [ProtoContract]
        public class TradeInfo
        {
            [ProtoMember(1)]
            [JsonProperty("price")]
            public double? Price { get; set; }

            [ProtoMember(2)]
            [JsonProperty("limitOrderId")]
            public string LimitOrderId { get; set; }

            [ProtoMember(3)]
            [JsonProperty("limitOrderExternalId")]
            public string LimitOrderExternalId { get; set; }

            [ProtoMember(4)]
            [JsonProperty("timestamp")]
            public DateTime Timestamp { get; set; }

            [ProtoMember(5)]
            [JsonProperty("marketClientId")]
            public string MarketClientId { get; set; }

            [ProtoMember(6)]
            [JsonProperty("marketAsset")]
            public string MarketAsset { get; set; }

            [ProtoMember(7)]
            [JsonProperty("marketVolume")]
            public double MarketVolume { get; set; }

            [ProtoMember(8)]
            [JsonProperty("limitClientId")]
            public string LimitClientId { get; set; }

            [ProtoMember(9)]
            [JsonProperty("limitVolume")]
            public double LimitVolume { get; set; }

            [ProtoMember(10)]
            [JsonProperty("limitAsset")]
            public string LimitAsset { get; set; }

            [ProtoMember(11)]
            [CanBeNull]
            [JsonProperty("feeInstruction")]
            public FeeInstruction FeeInstruction { get; set; }

            [ProtoMember(12)]
            [CanBeNull]
            [JsonProperty("feeTransfer")]
            public FeeTransfer FeeTransfer { get; set; }
        }
    }

    public static class Ext
    {
        public static ClientTrade[] GetTradeRecords(this TradeQueueItem.TradeInfo trade, IMarketOrder marketOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsMarket,
            IWalletCredentials walletCredentialsLimit)
        {
            var result = new List<ClientTrade>();

            result.AddRange(CreateTradeRecordsForClient(trade, marketOrder, btcTransactionId, walletCredentialsMarket, walletCredentialsLimit, true));
            result.AddRange(CreateTradeRecordsForClient(trade, marketOrder, btcTransactionId, walletCredentialsMarket, walletCredentialsLimit, false));

            return result.ToArray();
        }

        private static ClientTrade[] CreateTradeRecordsForClientWithVolumes(TradeQueueItem.TradeInfo trade, IMarketOrder marketOrder,
            string btcTransactionId, IWalletCredentials walletCredentialsMarket, IWalletCredentials walletCredentialsLimit,
            bool isMarketClient, double marketVolume, double limitVolume)
        {
            var clientId = isMarketClient ? walletCredentialsMarket?.ClientId : walletCredentialsLimit?.ClientId;

            if (!isMarketClient && string.IsNullOrWhiteSpace(clientId))
                return new ClientTrade[0];

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

            marketAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(marketAssetRecord.DateTime);
            limitAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(limitAssetRecord.DateTime);

            return new ClientTrade[] { marketAssetRecord, limitAssetRecord };
        }

        private static ClientTrade[] CreateTradeRecordsForClient(TradeQueueItem.TradeInfo trade, IMarketOrder marketOrder,
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

        public static ClientTrade[] ToDomainOffchain(this TradeQueueItem item, IWalletCredentials walletCredentialsMarket, IWalletCredentials walletCredentialsLimit,
            IReadOnlyCollection<Asset> assets)
        {
            var trade = item.Trades[0];

            var marketVolume = item.Trades.Sum(x => x.MarketVolume);
            var limitVolume = item.Trades.Sum(x => x.LimitVolume);

            var result = new List<ClientTrade>();

            result.AddRange(CreateTradeRecordsForClientWithVolumes(trade, item.Order, item.Order.Id, walletCredentialsMarket, walletCredentialsLimit, true, marketVolume, limitVolume));

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