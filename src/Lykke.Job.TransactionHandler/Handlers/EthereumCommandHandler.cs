using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.Blockchain;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Ethereum;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Services;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Handlers
{
    //Cashout operations from WalletApi
    public class EthereumCommandHandler
    {
        private readonly ILog _log;
        private readonly ISrvEthereumHelper _srvEthereumHelper;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly AppSettings.EthereumSettings _settings;
        private readonly TimeSpan _retryTimeout;
        private readonly IBcnClientCredentialsRepository _bcnClientCredentialsRepository;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly ITransactionService _transactionService;

        public EthereumCommandHandler(
            [NotNull] ILog log,
            [NotNull] ISrvEthereumHelper srvEthereumHelper,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IBcnClientCredentialsRepository bcnClientCredentialsRepository,
            [NotNull] IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            [NotNull] ITransactionService transactionService,
            [NotNull] AppSettings.EthereumSettings settings,
            TimeSpan retryTimeout)
        {
            _log = log.CreateComponentScope(nameof(EthereumCommandHandler));
            _srvEthereumHelper = srvEthereumHelper ?? throw new ArgumentNullException(nameof(srvEthereumHelper));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _bcnClientCredentialsRepository = bcnClientCredentialsRepository ?? throw new ArgumentNullException(nameof(bcnClientCredentialsRepository));
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository ?? throw new ArgumentNullException(nameof(ethereumTransactionRequestRepository));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _retryTimeout = retryTimeout;
        }

        public async Task<CommandHandlingResult> Handle(TransferEthereumCommand command, IEventPublisher eventPublisher)
        {
            var txRequest = await _ethereumTransactionRequestRepository.GetAsync(command.TransactionId);

            // todo: udpate txRequest in separated command
            var context = await _transactionService.GetTransactionContext<TransferContextData>(command.TransactionId.ToString());
            txRequest.OperationIds = new[] { context.Transfers[0].OperationId, context.Transfers[1].OperationId };
            await _ethereumTransactionRequestRepository.UpdateAsync(txRequest);

            ChaosKitty.Meow();

            var clientAddress = await _bcnClientCredentialsRepository.GetClientAddress(txRequest.ClientId);
            var hotWalletAddress = _settings.HotwalletAddress;

            string addressFrom;
            string addressTo;
            Guid transferId;
            string sign;
            switch (txRequest.OperationType)
            {
                case OperationType.TransferToTrusted:
                    addressFrom = clientAddress;
                    addressTo = hotWalletAddress;
                    transferId = txRequest.SignedTransfer.Id;
                    sign = txRequest.SignedTransfer.Sign;
                    break;
                case OperationType.TransferFromTrusted:
                    addressFrom = hotWalletAddress;
                    addressTo = clientAddress;
                    transferId = txRequest.Id;
                    sign = string.Empty;
                    break;
                case OperationType.TransferBetweenTrusted:
                    return CommandHandlingResult.Ok();
                default:
                    _log.WriteError(nameof(TransferEthereumCommand), "Unknown transfer type", null);
                    return CommandHandlingResult.Fail(_retryTimeout);
            }

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(txRequest.AssetId);
            var response = await _srvEthereumHelper.SendTransferAsync(transferId, sign, asset, addressFrom,
                addressTo, txRequest.Volume);

            ChaosKitty.Meow();

            if (response.HasError &&
                response.Error.ErrorCode != ErrorCode.OperationWithIdAlreadyExists &&
                response.Error.ErrorCode != ErrorCode.EntityAlreadyExists)
            {
                var errorMessage = response.Error.ToJson();
                _log.WriteError(nameof(TransferEthereumCommand), new Exception(errorMessage));
                return CommandHandlingResult.Fail(_retryTimeout);
            }

            eventPublisher.PublishEvent(new EthereumTransferSentEvent { TransferId = transferId });

            ChaosKitty.Meow();

            return CommandHandlingResult.Ok();
        }

    }
}
