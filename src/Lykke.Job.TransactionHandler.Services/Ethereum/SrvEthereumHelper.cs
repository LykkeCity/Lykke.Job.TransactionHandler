using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.EthereumCore.Client;
using Lykke.Service.EthereumCore.Client.Models;
using System;
using System.Threading.Tasks;
using ErrorResponse = Lykke.Job.TransactionHandler.Core.Services.Ethereum.ErrorResponse;

namespace Lykke.Job.TransactionHandler.Services.Ethereum
{
    public class SrvEthereumHelper : ISrvEthereumHelper
    {
        private readonly IEthereumCoreAPI _ethereumApi;

        public SrvEthereumHelper(IEthereumCoreAPI ethereumApi)
        {
            _ethereumApi = ethereumApi;
        }

        public async Task<EthereumResponse<OperationResponse>> SendTransferAsync(Guid id, string sign, Asset asset, string fromAddress, string toAddress, decimal amount)
        {
            var response = await _ethereumApi.ApiExchangeTransferPostAsync(new TransferModel
            {
                Amount = EthServiceHelpers.ConvertToContract(amount, asset.MultiplierPower, asset.Accuracy),
                CoinAdapterAddress = asset.AssetAddress,
                ToAddress = toAddress,
                FromAddress = fromAddress,
                Id = id,
                Sign = sign
            });

            if (response is ApiException error)
            {
                return new EthereumResponse<OperationResponse>
                {
                    Error = new ErrorResponse { Code = error.Error.Code.ToString(), Message = error.Error.Message }
                };
            }

            if (response is OperationIdResponse res)
            {
                return new EthereumResponse<OperationResponse> { Result = new OperationResponse { OperationId = res.OperationId } };
            }

            throw new Exception("Unknown response");
        }
    }
}