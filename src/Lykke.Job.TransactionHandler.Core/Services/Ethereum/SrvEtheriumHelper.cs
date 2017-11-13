using System;
using System.Threading.Tasks;
using Lykke.Service.Assets.Client.Models;

namespace Lykke.Job.TransactionHandler.Core.Services.Ethereum
{
    public interface ISrvEthereumHelper
    {
        Task<EthereumResponse<OperationResponse>> SendTransferAsync(Guid id, string sign, Asset asset, string fromAddress,
            string toAddress, decimal amount);

        Task<EthereumResponse<OperationResponse>> SendCashOutAsync(Guid id, string sign, Asset asset, string fromAddress,
            string toAddress, decimal amount);

        Task<EthereumResponse<OperationResponse>> SendTransferWithChangeAsync(decimal change, string signFrom, Guid id, Asset asset, string fromAddress,
            string toAddress, decimal amount);
    }

    #region Response Models

    public class EthereumTransaction
    {
        public string Hash { get; set; }
        public Guid OperationId { get; set; }
    }

    #endregion
}