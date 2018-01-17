using System.Collections.Generic;
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
        Task<IBcnCredentialsRecord> GetAsync(string clientId, string assetId);
        Task<IBcnCredentialsRecord> GetByAssetAddressAsync(string assetAddress);
        Task<IEnumerable<IBcnCredentialsRecord>> GetAsync(string clientId);
        Task<string> GetClientAddress(string clientId);
    }

}