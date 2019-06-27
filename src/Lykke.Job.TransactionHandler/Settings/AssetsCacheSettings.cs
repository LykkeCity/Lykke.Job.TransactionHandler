using System;
using JetBrains.Annotations;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class AssetsCacheSettings
    {
        public TimeSpan ExpirationPeriod { get; set; }
    }
}
