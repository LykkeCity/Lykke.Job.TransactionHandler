using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Service.Assets.Client;
using Lykke.Service.OperationsRepository.AutorestClient.Models;

namespace Lykke.Job.TransactionHandler.Queues.Models
{
    public class ClientTradesFactory : IClientTradesFactory
    {
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;

        public ClientTradesFactory(IAssetsServiceWithCache assetsServiceWithCache, IWalletCredentialsRepository walletCredentialsRepository)
        {
            _assetsServiceWithCache = assetsServiceWithCache;
            _walletCredentialsRepository = walletCredentialsRepository;
        }

        public async Task<ClientTrade[]> Create(string orderId, string clientId, TradeQueueItem.TradeInfo trade, double marketVolume, double limitVolume)
        {
            var walletCredsMarket = await _walletCredentialsRepository.GetAsync(trade.MarketClientId); // todo: Roman: HFT — get ClientId by WalletId?
            
            var result = new List<ClientTrade>();

            result.AddRange(CreateTradeRecordsForClientWithVolumes(trade, orderId, walletCredsMarket, marketVolume, limitVolume));

            var assets = await _assetsServiceWithCache.GetAllAssetsAsync();

            foreach (var clientTrade in result)
            {
                var asset = assets.FirstOrDefault(x => x.Id == clientTrade.AssetId);

                if (asset == null)
                    throw new ArgumentException("Unknown asset");

                // if client guarantee transaction or trusted asset, then it is already settled
                if (clientTrade.ClientId == clientId && clientTrade.Amount< 0 || asset.IsTrusted)
                    clientTrade.State = TransactionStates.SettledOffchain;
                else
                    clientTrade.State = TransactionStates.InProcessOffchain;
            }

            return result.ToArray();
        }

        private static ClientTrade[] CreateTradeRecordsForClientWithVolumes(TradeQueueItem.TradeInfo trade,
            string marketOrderId, IWalletCredentials walletCredentialsMarket, double marketVolume, double limitVolume)
        {
            var clientId = walletCredentialsMarket?.ClientId;

            if (string.IsNullOrWhiteSpace(clientId))
                return new ClientTrade[0];
            
            var marketAssetRecord = CreateCommonPartForTradeRecord(trade, marketOrderId);
            var limitAssetRecord = CreateCommonPartForTradeRecord(trade, marketOrderId);

            marketAssetRecord.ClientId = limitAssetRecord.ClientId = clientId; // todo: Roman: we don't need multisigs

            marketAssetRecord.Amount = marketVolume* -1;
            marketAssetRecord.AssetId = trade.MarketAsset;

            limitAssetRecord.Amount = limitVolume;
            limitAssetRecord.AssetId = trade.LimitAsset;

            marketAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(marketAssetRecord.DateTime);
            limitAssetRecord.Id = Core.Domain.CashOperations.Utils.GenerateRecordId(limitAssetRecord.DateTime);

            return new[] { marketAssetRecord, limitAssetRecord };
        }

        private static ClientTrade CreateCommonPartForTradeRecord(TradeQueueItem.TradeInfo trade, string marketOrderId)
        {
            return new ClientTrade
            {
                DateTime = trade.Timestamp,
                Price = trade.Price.GetValueOrDefault(),
                LimitOrderId = trade.LimitOrderExternalId,
                MarketOrderId = marketOrderId,
                TransactionId = marketOrderId
            };
        }
    }
} 