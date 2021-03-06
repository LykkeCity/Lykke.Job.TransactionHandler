﻿using System.Threading.Tasks;
using Common;
using Lykke.Job.TransactionHandler.Core.Contracts;
using Lykke.Job.TransactionHandler.Core.Services.Fee;

namespace Lykke.Job.TransactionHandler.Services
{
    public static class ServiceExtensions
    {
        public static Task<decimal> GetAmountNoFeeAsync(this IFeeCalculationService src, TransferQueueMessage message)
        {
            return src.GetAmountNoFeeAsync(message.Amount.ParseAnyDecimal(), message.AssetId, message.Fees, message.ToClientid);
        }
    }
}