using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems
{
    public interface IPaymentTransaction
    {
        string Id { get; }

        string ClientId { get; }

        double Amount { get; }

        string AssetId { get; }


        /// <summary>
        /// Amount of asset we deposit account
        /// </summary>
        double? DepositedAmount { get; }

        string DepositedAssetId { get; }


        double? Rate { get; }


        string AggregatorTransactionId { get; }

        DateTime Created { get; }

        PaymentStatus Status { get; }

        CashInPaymentSystem PaymentSystem { get; }

        string Info { get; }

    }


    public enum PaymentStatus
    {
        Created,
        NotifyProcessed,
        NotifyDeclined,
        Processing
    }


    public class PaymentTransaction : IPaymentTransaction
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public double Amount { get; set; }
        public string AssetId { get; set; }
        public double? DepositedAmount { get; set; }
        public string DepositedAssetId { get; set; }
        public double? Rate { get; set; }
        public string AggregatorTransactionId { get; set; }
        public DateTime Created { get; set; }
        public PaymentStatus Status { get; set; }
        public CashInPaymentSystem PaymentSystem { get; set; }
        public string Info { get; set; }

        public string OtherData { get; set; }


        public static PaymentTransaction Create(string id,
            CashInPaymentSystem paymentSystem,
            string clientId,
            double amount,
            string assetId,
            string assetToDeposit = null,
            string info = "",
            PaymentStatus status = PaymentStatus.Created)
        {

            return new PaymentTransaction
            {
                Id = id,
                PaymentSystem = paymentSystem,
                ClientId = clientId,
                Amount = amount,
                AssetId = assetId,
                Created = DateTime.UtcNow,
                Status = status,
                Info = info,
                DepositedAssetId = assetToDeposit ?? assetId
            };
        }
    }

    public interface IPaymentTransactionsRepository
    {
        Task CreateAsync(IPaymentTransaction paymentTransaction);

        Task<IPaymentTransaction> TryCreateAsync(IPaymentTransaction paymentTransaction);

        Task<IPaymentTransaction> SetStatus(string id, PaymentStatus status);

        Task<bool> CheckExistsAsync(IPaymentTransaction paymentTransaction);
    }
}