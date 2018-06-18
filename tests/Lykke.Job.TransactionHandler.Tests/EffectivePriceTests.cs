using System;
using System.Collections.Generic;
using System.Text;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Service.Assets.Client.Models;
using Xunit;

namespace Lykke.Job.TransactionHandler.Tests
{
    public class EffectivePriceTests
    {
        [Fact]
        public void Test_OneTrade_Limit()
        {
            var trades = new List<LimitQueueItem.LimitTradeInfo>()
            {
                new LimitQueueItem.LimitTradeInfo
                {
                    Volume = 1,
                    OppositeVolume = 8000,
                    Price = 9000,
                    Asset = "BTC",
                    OppositeAsset = "USD"
                }
            };

            Assert.Equal(9000, ConvertExtensions.CalcEffectivePrice(trades, GetDefaultPair(), false));
        }

        [Fact]
        public void Test_OneTrade_Market()
        {
            var trades = new List<TradeQueueItem.TradeInfo>()
            {
                new TradeQueueItem.TradeInfo
                {
                    MarketVolume = 1,
                    LimitVolume = 8000,
                    Price = 9000,
                    MarketAsset = "BTC",
                    LimitAsset = "USD"
                }
            };

            Assert.Equal(9000, ConvertExtensions.CalcEffectivePrice(trades, GetDefaultPair(), false));
        }

        [Fact]
        public void Test_ManyTrades_Limit()
        {
            var trades = new List<LimitQueueItem.LimitTradeInfo>()
            {
                new LimitQueueItem.LimitTradeInfo
                {
                    Volume = 0.9,
                    OppositeVolume = 8100,
                    Price = 9000,
                    Asset = "BTC",
                    OppositeAsset = "USD"
                },
                new LimitQueueItem.LimitTradeInfo
                {
                    Volume = 0.3,
                    OppositeVolume = 2745,
                    Price = 9150,
                    Asset = "BTC",
                    OppositeAsset = "USD"
                },
                new LimitQueueItem.LimitTradeInfo
                {
                    Volume = 0.1,
                    OppositeVolume = 920,
                    Price = 9200,
                    Asset = "BTC",
                    OppositeAsset = "USD"
                }
            };

            Assert.Equal(9050, ConvertExtensions.CalcEffectivePrice(trades, GetDefaultPair(), false));
        }

        [Fact]
        public void Test_ManyTrades_Market()
        {
            var trades = new List<TradeQueueItem.TradeInfo>()
            {
                new TradeQueueItem.TradeInfo
                {
                    MarketVolume = 0.2,
                    LimitVolume = 1800,
                    Price = 9000,
                    MarketAsset = "BTC",
                    LimitAsset = "USD"
                },
                new TradeQueueItem.TradeInfo
                {
                    MarketVolume = 0.3,
                    LimitVolume = 2745,
                    Price = 9150,
                    MarketAsset = "BTC",
                    LimitAsset = "USD"
                }
            };

            Assert.Equal(9090, ConvertExtensions.CalcEffectivePrice(trades, GetDefaultPair(), false));
        }

        [Fact]
        public void Test_Trade_With_Zero_Volume()
        {
            var trades = new List<TradeQueueItem.TradeInfo>()
            {
                new TradeQueueItem.TradeInfo
                {
                    MarketVolume = 0.000001,
                    LimitVolume = 0,
                    Price = 9000,
                    MarketAsset = "BTC",
                    LimitAsset = "USD"
                },
                new TradeQueueItem.TradeInfo
                {
                    MarketVolume = 0.3,
                    LimitVolume = 2745,
                    Price = 9150,
                    MarketAsset = "BTC",
                    LimitAsset = "USD"
                }
            };

            Assert.Equal(9150, ConvertExtensions.CalcEffectivePrice(trades, GetDefaultPair(), false));
        }

        [Fact]
        public void Test_Trade_Price_Rounding()
        {
            var pair = GetDefaultPair();

            var trades = new List<TradeQueueItem.TradeInfo>()
            {
                new TradeQueueItem.TradeInfo
                {
                    MarketVolume = 1,
                    LimitVolume = 10000,
                    Price = 10000,
                    MarketAsset = "BTC",
                    LimitAsset = "USD"
                },
                new TradeQueueItem.TradeInfo
                {
                    MarketVolume = 2,
                    LimitVolume = 22000,
                    Price = 11000,
                    MarketAsset = "BTC",
                    LimitAsset = "USD"
                }
            };

            Assert.Equal(10666.666, ConvertExtensions.CalcEffectivePrice(trades, pair, false), pair.Accuracy);
            Assert.Equal(10666.667, ConvertExtensions.CalcEffectivePrice(trades, pair, true), pair.Accuracy);
        }

        private AssetPair GetDefaultPair()
        {
            return new AssetPair
            {
                Id = "BTCUSD",
                Accuracy = 3,
                BaseAssetId = "BTC",
                QuotingAssetId = "USD"
            };
        }
    }
}
