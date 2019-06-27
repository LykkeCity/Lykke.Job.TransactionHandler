using JetBrains.Annotations;

namespace Lykke.Job.TransactionHandler.Settings
{
    [UsedImplicitly]
    public class EthereumSettings
    {
        public string EthereumCoreUrl { get; set; }
        public string HotwalletAddress { get; set; }
    }
}
