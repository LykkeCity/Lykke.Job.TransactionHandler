using JetBrains.Annotations;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class ServiceSettings
    {
        [HttpCheck("/api/isalive")]
        public string OperationsUrl { get; set; }
    }
}
