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
    }
}