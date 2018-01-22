using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.AzureRepositories;
using Lykke.Job.TransactionHandler.Commands.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.Offchain;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.Offchain;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.Operations.Client;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class TransferSaga
    {
        private readonly ILog _log;
        private readonly IOffchainRequestService _offchainRequestService;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly IOperationsClient _operationsClient;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly ITransactionService _transactionService;

        public TransferSaga(
            [NotNull] ILog log,
            [NotNull] IOffchainRequestService offchainRequestService,
            [NotNull] IClientAccountClient clientAccountClient,
            [NotNull] IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            [NotNull] IOperationsClient operationsClient,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] ITransactionService transactionService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _offchainRequestService = offchainRequestService ?? throw new ArgumentNullException(nameof(offchainRequestService));
            _clientAccountClient = clientAccountClient ?? throw new ArgumentNullException(nameof(clientAccountClient));
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository ?? throw new ArgumentNullException(nameof(ethereumTransactionRequestRepository));
            _operationsClient = operationsClient ?? throw new ArgumentNullException(nameof(operationsClient));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        }

        private async Task Handle(TransferOperationStateSavedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(TransferSaga), nameof(TransferOperationStateSavedEvent), evt.ToJson());

            var transactionId = evt.TransactionId;
            var queueMessage = evt.QueueMessage;
            var amount = queueMessage.Amount.ParseAnyDouble();

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(queueMessage.AssetId);

            if (!(await _clientAccountClient.IsTrustedAsync(queueMessage.ToClientid)).Value
                && asset.Blockchain == Blockchain.Bitcoin
                && !asset.IsTrusted)
            {
                try
                {
                    await _offchainRequestService.CreateOffchainRequestAndNotify(
                        transactionId: transactionId,
                        clientId: queueMessage.ToClientid,
                        assetId: queueMessage.AssetId,
                        amount: (decimal)amount,
                        orderId: null,
                        type: OffchainTransferType.CashinToClient);
                }
                catch (Microsoft.WindowsAzure.Storage.StorageException exception)
                {
                    if (exception.RequestInformation.HttpStatusCode != AzureHelper.ConflictStatusCode)
                        throw;

                    await _log.WriteWarningAsync(nameof(TransferSaga), nameof(TransferOperationStateSavedEvent), "",
                        $"Transfer already exists {transactionId}");
                }
            }

            // handling of ETH transfers to trusted wallets if it is ETH transfer
            var ethTxRequest = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(transactionId));
            if (ethTxRequest != null)
            {
                var context = await _transactionService.GetTransactionContext<TransferContextData>(transactionId);

                ethTxRequest.OperationIds = new[] { context.Transfers[0].OperationId, context.Transfers[1].OperationId };
                await _ethereumTransactionRequestRepository.UpdateAsync(ethTxRequest);

                var cmd = new EthTransferTrustedWalletCommand
                {
                    TxRequest = ethTxRequest
                };

                switch (ethTxRequest.OperationType)
                {
                    case OperationType.TransferToTrusted:
                        cmd.TransferType = TransferType.ToTrustedWallet;
                        sender.SendCommand(cmd, BoundedContexts.Ethereum);
                        break;
                    case OperationType.TransferFromTrusted:
                        cmd.TransferType = TransferType.FromTrustedWallet;
                        sender.SendCommand(cmd, BoundedContexts.Ethereum);
                        break;
                    case OperationType.TransferBetweenTrusted:
                        cmd.TransferType = TransferType.BetweenTrusted;
                        sender.SendCommand(cmd, BoundedContexts.Ethereum);
                        break;
                }
            }
            else
            {
                await _operationsClient.Complete(new Guid(transactionId));
            }
        }
    }
}