using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.TransactionHandler.Core.Services.MarginTrading;
using Lykke.Job.TransactionHandler.Services.Generated.MarginApi;
using Lykke.Job.TransactionHandler.Services.Generated.MarginApi.Models;
using Microsoft.Rest;

namespace Lykke.Job.TransactionHandler.Services.MarginTrading
{
    public class MarginDataServiceSettings
    {
        public Uri BaseUri { get; set; }
        public string ApiKey { get; set; }
    }

    public class MarginDataService : IMarginDataService
    {
        private readonly MarginDataServiceSettings _settings;
        private readonly ILog _log;

        public MarginDataService(MarginDataServiceSettings settings, ILog log)
        {
            _settings = settings;
            _log = log;
        }

        private MarginTradingApi Api => new MarginTradingApi(_settings.BaseUri);

        public async Task<OperationResult> DepositToAccount(string clientId, string accountId, double amount,
            MarginPaymentType paymentType)
        {
            var request = new AccountDepositWithdrawRequest
            {
                AccountId = accountId,
                ClientId = clientId,
                Amount = amount,
                PaymentType = paymentType.ConvertToDto()
            };

            try
            {
                var result = await Api.ApiBackofficeMarginTradingAccountsDepositPostWithHttpMessagesAsync(
                    _settings.ApiKey,
                    request);

                if (result.Body == true)
                    return OperationResult.Success();
                
                return OperationResult.Error("Error deposit to margin account");
            }
            catch (HttpOperationException e)
            {
                return GetOperationResult(e.Response.Content);
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(nameof(MarginDataService), "Deposit to margin account", request.ToJson(), ex);
                return OperationResult.Error($"Margin trading call error: {ex.Message}");
            }
        }

        private OperationResult GetOperationResult(string response)
        {
            try
            {
                var mtResponse = response.DeserializeJson<MtBackendResponse<string>>();
                return OperationResult.Error(mtResponse.Message ?? mtResponse.Result);
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(MarginDataService), "Parse margin trading response", response, ex);
                return OperationResult.Error($"Parse margin trading response error: {ex.Message}");
            }
        }
    }

    public class MtBackendResponse<T>
    {
        public T Result { get; set; }
        public string Message { get; set; }
    }
}