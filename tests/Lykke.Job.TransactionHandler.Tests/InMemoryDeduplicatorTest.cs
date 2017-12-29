using System;
using System.Collections.Generic;
using System.Threading;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Services;
using Xunit;

namespace Lykke.Job.TransactionHandler.Tests
{
    public class InMemoryDeduplicatorTest
    {
        [Fact]
        public void EnsureNotDuplicate()
        {
            var value = new TradeQueueItem
            {
                Order = new TradeQueueItem.MarketOrder(),
                Trades = new List<TradeQueueItem.TradeInfo>
            {
                new TradeQueueItem.TradeInfo{FeeTransfer = new FeeTransfer{Volume = 1.1}}
            }
            };
            var sameValue = new TradeQueueItem
            {
                Order = new TradeQueueItem.MarketOrder(),
                Trades = new List<TradeQueueItem.TradeInfo>
                {
                    new TradeQueueItem.TradeInfo{FeeTransfer = new FeeTransfer{Volume = 1.1}}
                }
            };
            var differentValue = new TradeQueueItem
            {
                Order = new TradeQueueItem.MarketOrder(),
                Trades = new List<TradeQueueItem.TradeInfo>
                {
                    new TradeQueueItem.TradeInfo{FeeTransfer = new FeeTransfer{Volume = 1.2}}
                }
            };

            var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
            var cacheInvalidationTimeout = TimeSpan.FromSeconds(0.5);
            var deduplicator = new InMemoryDeduplicator(cache, cacheInvalidationTimeout);

            Assert.True(deduplicator.EnsureNotDuplicate(value));
            Assert.False(deduplicator.EnsureNotDuplicate(value));
            Assert.False(deduplicator.EnsureNotDuplicate(sameValue));
            Assert.True(deduplicator.EnsureNotDuplicate(differentValue));

            Thread.Sleep(cacheInvalidationTimeout);
            Assert.True(deduplicator.EnsureNotDuplicate(value));
        }
    }
}
