using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Services.TrustedWallet;
using Lykke.MatchingEngine.Connector.Abstractions.Services;

namespace Lykke.Job.TransactionHandler.Services.TrustedWallet
{
    public class TrustedWalletService : ITrustedWalletService
    {
        private readonly ILog _log;
        private readonly IMatchingEngineClient _matchingEngineClient;

        public TrustedWalletService(IMatchingEngineClient matchingEngineClient, ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _matchingEngineClient = matchingEngineClient ?? throw new ArgumentNullException(nameof(matchingEngineClient));
        }

        public async Task<bool> Deposit(string walletId, string assetId, decimal amount)
        {
            var id = GetNextRequestId();
            var result = await _matchingEngineClient.CashInOutAsync(id, walletId, assetId, (double)amount);
            if (result == null || result.Status != Lykke.MatchingEngine.Connector.Abstractions.Models.MeStatusCodes.Ok)
            {
                await
                    _log.WriteWarningAsync(nameof(TrustedWalletService), nameof(Deposit), "ME CashInOut error",
                        result?.ToJson());
            }
            return result != null;
        }

        public async Task<bool> Withdraw(string walletId, string assetId, decimal amount)
        {
            var id = GetNextRequestId();
            var result = await _matchingEngineClient.CashInOutAsync(id, walletId, assetId, (double)-amount);
            if (result == null || result.Status != Lykke.MatchingEngine.Connector.Abstractions.Models.MeStatusCodes.Ok)
            {
                await
                    _log.WriteWarningAsync(nameof(TrustedWalletService), nameof(Withdraw), "ME CashInOut error",
                        result?.ToJson());
            }
            return result != null;
        }

        private string GetNextRequestId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
