using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Services.TrustedWallet
{
    public interface ITrustedWalletService
    {
        Task<bool> Deposit(string walletId, string assetId, decimal amount);
        Task<bool> Withdraw(string walletId, string assetId, decimal amount);
    }
}
