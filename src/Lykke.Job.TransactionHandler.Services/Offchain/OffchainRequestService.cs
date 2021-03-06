﻿using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;

namespace Lykke.Job.TransactionHandler.Services.Offchain
{
    public class OffchainRequestService : IOffchainRequestService
    {
        private readonly IOffchainRequestRepository _offchainRequestRepository;
        private readonly IOffchainTransferRepository _offchainTransferRepository;

        public OffchainRequestService(
            IOffchainRequestRepository offchainRequestRepository,
            IOffchainTransferRepository offchainTransferRepository)
        {
            _offchainRequestRepository = offchainRequestRepository;
            _offchainTransferRepository = offchainTransferRepository;
        }

        public async Task CreateOffchainRequestAndNotify(
            string transactionId,
            string clientId,
            string assetId,
            decimal amount,
            string orderId,
            OffchainTransferType type)
        {
            await _offchainTransferRepository.CreateTransfer(transactionId, clientId, assetId, amount, type, null, orderId);
            await _offchainRequestRepository.CreateRequest(transactionId, clientId, assetId, RequestType.RequestTransfer, type, null);
        }
    }
}