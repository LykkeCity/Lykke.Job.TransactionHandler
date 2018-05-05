using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.ClientAccount.Client;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class TransferSaga
    {
        private readonly ILog _log;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;

        public TransferSaga(
            [NotNull] ILog log,
            [NotNull] IClientAccountClient clientAccountClient,
            [NotNull] IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clientAccountClient = clientAccountClient ?? throw new ArgumentNullException(nameof(clientAccountClient));
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository ?? throw new ArgumentNullException(nameof(ethereumTransactionRequestRepository));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
        }

        public async Task Handle(TransferOperationStateSavedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(TransferSaga), nameof(TransferOperationStateSavedEvent), evt.ToJson());

            var transactionId = evt.TransactionId;
            var queueMessage = evt.QueueMessage;
            var amountNoFee = evt.AmountNoFee;

            var asset = await _assetsServiceWithCache.TryGetAssetAsync(queueMessage.AssetId);

            if (!(await _clientAccountClient.IsTrustedAsync(queueMessage.ToClientid)).Value
                && asset.Blockchain == Blockchain.Bitcoin
                && !asset.IsTrusted)
            {
                sender.SendCommand(
                    new CreateOffchainCashinRequestCommand
                    {
                        Id = transactionId,
                        ClientId = queueMessage.ToClientid,
                        Amount = (decimal)amountNoFee,
                        AssetId = queueMessage.AssetId
                    },
                    BoundedContexts.Offchain);
            }

            // handling of ETH transfers to trusted wallets if it is ETH transfer
            var ethTxRequest = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(transactionId));
            if (ethTxRequest != null)
            {
                sender.SendCommand(
                    new TransferEthereumCommand { TransactionId = Guid.Parse(transactionId) },
                    BoundedContexts.Ethereum);
            }
            else
            {
                sender.SendCommand(
                    new CompleteOperationCommand { CommandId = new Guid(transactionId) },
                    BoundedContexts.Operations);
            }
        }

        public async Task Handle(EthereumTransferSentEvent evt, ICommandSender sender)
        {
            sender.SendCommand(
                new CompleteOperationCommand { CommandId = evt.TransferId },
                BoundedContexts.Operations);
        }
    }
}
