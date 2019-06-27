using JetBrains.Annotations;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class AssetsSettings
    {
        [HttpCheck("/api/isalive")]
        public string ServiceUrl { get; set; }
    }
}
