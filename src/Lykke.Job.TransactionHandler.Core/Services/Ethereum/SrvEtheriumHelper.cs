using Lykke.Service.Assets.Client.Models;
using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Services.Ethereum
{
    public interface ISrvEthereumHelper
    {
        Task<EthereumResponse<OperationResponse>> SendTransferAsync(Guid id, string sign, Asset asset, string fromAddress,
            string toAddress, decimal amount);
    }
}