using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Service.Assets.Client.Models;
using Nethereum.Util;
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
        private readonly AddressUtil _addressUtil;

        public SrvEthereumHelper(IEthereumApi ethereumApi, Lykke.Service.Assets.Client.IAssetsService assetsService)
        {
            _addressUtil = new AddressUtil();
            _ethereumApi = ethereumApi;
            _assetsService = assetsService;
        }

        public bool IsValidAddress(string address)
        {
            if (new Regex("!^(0x)?[0-9a-f]{40}$", RegexOptions.IgnoreCase).IsMatch(address))
            {
                // check if it has the basic requirements of an address
                return false;
            }
            else if (new Regex("^(0x)?[0-9a-f]{40}$").IsMatch(address) ||
                     new Regex("^(0x)?[0-9A-F]{40}$").IsMatch(address))
            {
                // If it's all small caps or all all caps, return true
                return true;
            }
            else
            {
                // Check each case
                return _addressUtil.IsChecksumAddress(address);
            }
        }

        public async Task<EthereumResponse<GetContractModel>> GetContractAsync(Asset asset, string userAddress)
        {
            var response = await _ethereumApi.ApiTransitionCreatePostAsync(new CreateTransitionContractModel
            {
                CoinAdapterAddress = asset.AssetAddress,
                UserAddress = userAddress
            });

            var error = response as ApiException;
            if (error != null)
            {
                return new EthereumResponse<GetContractModel>
                {
                    Error = new ErrorResponse { Code = error.Error.Code, Message = error.Error.Message }
                };
            }

            var res = response as RegisterResponse;
            if (res != null)
            {
                return new EthereumResponse<GetContractModel>
                {
                    Result = new GetContractModel
                    {
                        Contract = res.Contract
                    }
                };
            }

            throw new Exception("Unknown response");
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

        public async Task<EthereumResponse<EthereumTransaction>> GetNewTxHashAndIdAsync(Asset asset, string fromAddress, string toAddress, decimal amount)
        {
            var response = await _ethereumApi.ApiHashCalculateAndGetIdPostAsync(new BaseCoinRequestParametersModel
            {
                Amount = EthServiceHelpers.ConvertToContract(amount, asset.MultiplierPower, asset.Accuracy),
                CoinAdapterAddress = asset.AssetAddress,
                FromAddress = fromAddress,
                ToAddress = toAddress
            });

            var error = response as ApiException;
            if (error != null)
            {
                return new EthereumResponse<EthereumTransaction>
                {
                    Error = new ErrorResponse { Code = error.Error.Code, Message = error.Error.Message }
                };
            }

            var res = response as HashResponseWithId;
            if (res != null)
            {
                return new EthereumResponse<EthereumTransaction>
                {
                    Result = new EthereumTransaction
                    {
                        Hash = res.HashHex,
                        OperationId = res.OperationId.GetValueOrDefault()
                    }
                };
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

        public async Task<EthereumResponse<EstimationResponse>> EstimateCashOutAsync(Guid id, string sign, Asset asset, string fromAddress, string toAddress, decimal amount)
        {
            var response = await _ethereumApi.ApiExchangeEstimateCashoutGasPostAsync(new TransferModel
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
                return new EthereumResponse<EstimationResponse>
                {
                    Error = new ErrorResponse { Code = error.Error.Code, Message = error.Error.Message }
                };
            }

            EstimatedGasModel res = response as EstimatedGasModel;
            if (res != null)
            {
                return new EthereumResponse<EstimationResponse>
                {
                    Result = new EstimationResponse
                    {
                        GasAmount = res.EstimatedGas,
                        IsAllowed = res.IsAllowed.Value
                    }
                };
            }

            throw new Exception("Unknown response");
        }

        public async Task<EthereumResponse<bool>> IsSignValid(Guid id, string sign, Asset asset, string fromAddress, string toAddress, decimal amount)
        {
            var response = await _ethereumApi.ApiExchangeCheckSignPostAsync(new CheckSignModel
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
                return new EthereumResponse<bool>
                {
                    Error = new ErrorResponse { Code = error.Error.Code, Message = error.Error.Message }
                };
            }

            var res = response as CheckSignResponse;
            if (res != null)
            {
                return new EthereumResponse<bool>
                {
                    Result = res.SignIsCorrect.GetValueOrDefault()

                };
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

        public async Task<EthereumResponse<bool>> IsSynced(string coinAdapterAddress, string userAddress)
        {
            var response = await _ethereumApi.ApiExchangeCheckPendingTransactionPostAsync(new CheckPendingModel
            {
                UserAddress = userAddress,
                CoinAdapterAddress = coinAdapterAddress
            });

            var error = response as ApiException;
            if (error != null)
            {
                return new EthereumResponse<bool>
                {
                    Error = new ErrorResponse { Code = error.Error.Code, Message = error.Error.Message }
                };
            }

            var res = response as CheckPendingResponse;
            if (res != null)
            {
                return new EthereumResponse<bool>
                {
                    Result = res.IsSynced

                };
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