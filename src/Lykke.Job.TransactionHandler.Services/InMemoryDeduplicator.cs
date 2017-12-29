using System;
using Lykke.Job.TransactionHandler.Core.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Lykke.Job.TransactionHandler.Services
{
    public class InMemoryDeduplicator : IDeduplicator
    {
        private static readonly TimeSpan DefaultCacheInvalidationTimeout = TimeSpan.FromMinutes(10);
        private readonly IMemoryCache _cache;
        private readonly MemoryCacheEntryOptions _cacheEntryOptions;

        public InMemoryDeduplicator(IMemoryCache cache, TimeSpan? cacheInvalidationTimeout = null)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(cacheInvalidationTimeout ?? DefaultCacheInvalidationTimeout);
        }

        public bool EnsureNotDuplicate(object value)
        {
            var key = NetJSON.NetJSON.Serialize(value);
            if (_cache.TryGetValue(key, out bool _))
            {
                return false;
            }

            _cache.Set(key, true, _cacheEntryOptions);
            return true;
        }
    }
}
