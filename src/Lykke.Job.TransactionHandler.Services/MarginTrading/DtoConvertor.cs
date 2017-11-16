using System;
using Lykke.Job.TransactionHandler.Core.Services.MarginTrading;
using Lykke.Job.TransactionHandler.Services.Generated.MarginApi.Models;

namespace Lykke.Job.TransactionHandler.Services.MarginTrading
{
    internal static class DtoConvertor
    {
        public static PaymentType ConvertToDto(this MarginPaymentType paymentType)
        {
            switch (paymentType)
            {
                case MarginPaymentType.Swift:
                    return PaymentType.Swift;

                case MarginPaymentType.Transfer:
                    return PaymentType.Transfer;
            }

            throw new ArgumentOutOfRangeException(nameof(paymentType), paymentType, string.Empty);
        }
    }
}