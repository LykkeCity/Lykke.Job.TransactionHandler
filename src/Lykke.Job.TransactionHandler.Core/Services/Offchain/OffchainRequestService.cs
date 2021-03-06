﻿using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;

namespace Lykke.Job.TransactionHandler.Core.Services.Offchain
{
    public interface IOffchainRequestService
    {
        Task CreateOffchainRequestAndNotify(string transactionId, string clientId, string assetId, decimal amount, string orderId, OffchainTransferType type);
    }
}
