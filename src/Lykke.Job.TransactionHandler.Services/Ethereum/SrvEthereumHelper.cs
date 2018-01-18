using System;
using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Service.Assets.Client.Models;
using ErrorResponse = Lykke.Job.TransactionHandler.Core.Services.Ethereum.ErrorResponse;
using Lykke.Service.Assets.Client;
using System.Collections.Generic;
using System.Linq;
using Lykke.Service.EthereumCore.Client.Models;
using Lykke.Service.EthereumCore.Client;

namespace Lykke.Job.TransactionHandler.Services.Ethereum
{
    public class SrvEthereumHelper : ISrvEthereumHelper
    {
        private readonly IEthereumApi _ethereumApi;
        private readonly IAssetsService _assetsService;

        public SrvEthereumHelper(IEthereumApi ethereumApi, IAssetsService assetsService)
        {
            _ethereumApi = ethereumApi;
            _assetsService = assetsService;
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

            var error = response as ApiException;
            if (error != null)
            {
                return new EthereumResponse<OperationResponse>
                {
                    Error = new ErrorResponse { Code = error.Error.Code, Message = error.Error.Message }
                };
            }

            var res = response as OperationIdResponse;
            if (res != null)
            {
                return new EthereumResponse<OperationResponse> { Result = new OperationResponse { OperationId = res.OperationId } };
            }

            throw new Exception("Unknown response");
        }

        public async Task<EthereumResponse<OperationResponse>> SendCashOutAsync(Guid id, string sign, Asset asset, string fromAddress, string toAddress, decimal amount)
        {
            var response = await _ethereumApi.ApiExchangeCashoutPostAsync(new CashoutModel
            {
                Amount = EthServiceHelpers.ConvertToContract(amount, asset.MultiplierPower, asset.Accuracy),
                CoinAdapterAddress = asset.AssetAddress,
                ToAddress = toAddress,
                FromAddress = fromAddress,
                Id = id,
                Sign = sign
            });

            var error = response as ApiException;
            if (error != null)
            {
                return new EthereumResponse<OperationResponse>
                {
                    Error = new ErrorResponse { Code = error.Error.Code, Message = error.Error.Message }
                };
            }

            var res = response as OperationIdResponse;
            if (res != null)
            {
                return new EthereumResponse<OperationResponse> { Result = new OperationResponse { OperationId = res.OperationId } };
            }

            throw new Exception("Unknown response");
        }

        public async Task<EthereumResponse<OperationResponse>> SendTransferWithChangeAsync(decimal change, string signFrom, Guid id, Asset asset, string fromAddress,
            string toAddress, decimal amount)
        {
            var response = await _ethereumApi.ApiExchangeTransferWithChangePostAsync(new TransferWithChangeModel
            {
                Change = EthServiceHelpers.ConvertToContract(change, asset.MultiplierPower, asset.Accuracy),
                Amount = EthServiceHelpers.ConvertToContract(amount, asset.MultiplierPower, asset.Accuracy),
                SignFrom = signFrom,
                CoinAdapterAddress = asset.AssetAddress,
                ToAddress = toAddress,
                FromAddress = fromAddress,
                Id = id
            });

            var error = response as ApiException;
            if (error != null)
            {
                return new EthereumResponse<OperationResponse>
                {
                    Error = new ErrorResponse { Code = error.Error.Code, Message = error.Error.Message }
                };
            }

            var res = response as OperationIdResponse;
            if (res != null)
            {
                return new EthereumResponse<OperationResponse> { Result = new OperationResponse { OperationId = res.OperationId } };
            }

            throw new Exception("Unknown response");
        }

        public async Task<EthereumResponse<bool>> HotWalletCashoutAsync(string opId, string fromAddress,
             string toAddress, decimal amount, Asset asset)
        {
            var token = await _assetsService.Erc20TokenGetBySpecificationAsync(new Erc20TokenSpecification(new List<string>() { asset.Id }));
            var tokenAddress = token?.Items?.FirstOrDefault()?.Address;

            if (string.IsNullOrEmpty(tokenAddress))
            {
                throw new Exception("Can't perform cashout on empty token");
            }

            var response = await _ethereumApi.ApiHotWalletPostAsync(new HotWalletCashoutRequest(opId,
            fromAddress,
            toAddress,
            EthServiceHelpers.ConvertToContract(amount, asset.MultiplierPower, asset.Accuracy),
            tokenAddress));

            if (response != null)
            {
                return new EthereumResponse<bool>
                {
                    Error = new ErrorResponse { Code = response.Error.Code, Message = response.Error.Message }
                };
            }

            return new EthereumResponse<bool>
            {
                Result = true
            };
        }
    }
}