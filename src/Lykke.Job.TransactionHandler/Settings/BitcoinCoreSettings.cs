using JetBrains.Annotations;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class BitcoinCoreSettings
    {
        [HttpCheck("/api/isalive")]
        public string BitcoinCoreApiUrl { get; set; }
    }
}
