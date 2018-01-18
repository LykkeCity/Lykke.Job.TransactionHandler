using System;
using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;

namespace Lykke.Job.TransactionHandler.Services.Offchain
{
    public class OffchainRequestService : IOffchainRequestService
    {
        private readonly IOffchainRequestRepository _offchainRequestRepository;
        private readonly IOffchainTransferRepository _offchainTransferRepository;
        private readonly INotificationsService _notificationsService;

        public OffchainRequestService(
            IOffchainRequestRepository offchainRequestRepository,
            IOffchainTransferRepository offchainTransferRepository,
            INotificationsService notificationsService)
        {
            _offchainRequestRepository = offchainRequestRepository;
            _offchainTransferRepository = offchainTransferRepository;
            _notificationsService = notificationsService;
        }

        public async Task CreateOffchainRequest(string transactionId, string clientId, string assetId, decimal amount, string orderId, OffchainTransferType type)
        {
            await _offchainTransferRepository.CreateTransfer(transactionId, clientId, assetId, amount, type, null, orderId);

            await _offchainRequestRepository.CreateRequest(transactionId, clientId, assetId, RequestType.RequestTransfer, type, null);
        }

        public async Task CreateOffchainRequestAndNotify(string transactionId, string clientId, string assetId, decimal amount,
            string orderId, OffchainTransferType type)
        {
            await CreateOffchainRequest(transactionId, clientId, assetId, amount, orderId, type);
            await _notificationsService.OffchainNotifyUser(clientId);
        }

        public Task CreateOffchainRequestAndLock(string transactionId, string clientId, string assetId, decimal amount, string orderId,
            OffchainTransferType type)
        {
            return CreateAndAggregate(transactionId, clientId, assetId, amount, orderId, type, DateTime.UtcNow);
        }

        public async Task CreateOffchainRequestAndUnlock(string transactionId, string clientId, string assetId, decimal amount,
            string orderId, OffchainTransferType type)
        {
            await CreateAndAggregate(transactionId, clientId, assetId, amount, orderId, type, null);
            await _notificationsService.OffchainNotifyUser(clientId);
        }

        private async Task CreateAndAggregate(string transactionId, string clientId, string assetId, decimal amount,
            string orderId, OffchainTransferType type, DateTime? lockTime)
        {
            var newTransfer = await _offchainTransferRepository.CreateTransfer(transactionId, clientId, assetId, amount, type, null, orderId);

            var request = await _offchainRequestRepository.CreateRequestAndLock(transactionId, clientId, assetId, RequestType.RequestTransfer, type, lockTime);

            var oldTransferId = request.TransferId;

            //aggregate transfers
            if (oldTransferId != newTransfer.Id)
            {
                await _offchainTransferRepository.AddChildTransfer(oldTransferId, newTransfer);
                await _offchainTransferRepository.SetTransferIsChild(newTransfer.Id, oldTransferId);
            }
        }
    }
}