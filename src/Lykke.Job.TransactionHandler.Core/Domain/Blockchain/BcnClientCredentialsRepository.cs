using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Blockchain
{
    public interface IBcnCredentialsRecord
    {
        string Address { get; set; }
        string EncodedKey { get; set; }
        string PublicKey { get; set; }
        string AssetId { get; set; }
        string ClientId { get; set; }
        string AssetAddress { get; set; }
    }

    public interface IBcnClientCredentialsRepository
    {
        Task<IBcnCredentialsRecord> GetByAssetAddressAsync(string assetAddress);
        Task<string> GetClientAddress(string clientId);
    }
}