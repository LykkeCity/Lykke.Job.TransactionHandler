using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Cqrs;
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
            ILog log,
            IOffchainRequestService offchainRequestService,
            IClientAccountClient clientAccountClient,
            IEthereumTransactionRequestRepository ethereumTransactionRequestRepository,
            IOperationsClient operationsClient,
            IAssetsServiceWithCache assetsServiceWithCache, ITransactionService transactionService)
        {
            _log = log;
            _offchainRequestService = offchainRequestService;
            _clientAccountClient = clientAccountClient;
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository;
            _operationsClient = operationsClient;
            _assetsServiceWithCache = assetsServiceWithCache;
            _transactionService = transactionService;
        }

        private async Task Handle(TransferCreatedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(TransferSaga), nameof(TransferCreatedEvent), evt.ToJson());

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
                    await _offchainRequestService.CreateOffchainRequestAndNotify(transactionId,
                        queueMessage.ToClientid, queueMessage.AssetId, (decimal)amount, null,
                        OffchainTransferType.CashinToClient);
                }
                catch (Exception)
                {
                    await _log.WriteWarningAsync(nameof(TransferSaga), nameof(TransferCreatedEvent), "",
                        $"Transfer already exists {transactionId}");
                }
            }

            var context = await GetContext(evt.TransactionId);

            // handling of ETH transfers to trusted wallets if it is ETH transfer
            var ethTxRequest = await _ethereumTransactionRequestRepository.GetAsync(Guid.Parse(queueMessage.Id));
            if (ethTxRequest != null)
            {
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
                        sender.SendCommand(cmd, "transfers");
                        break;
                    case OperationType.TransferFromTrusted:
                        cmd.TransferType = TransferType.FromTrustedWallet;
                        sender.SendCommand(cmd, "transfers");
                        break;
                    case OperationType.TransferBetweenTrusted:
                        cmd.TransferType = TransferType.BetweenTrusted;
                        sender.SendCommand(cmd, "transfers");
                        break;
                }
            }
            else
            {
                await _operationsClient.Complete(new Guid(transactionId));
            }
        }

        private async Task<TransferContextData> GetContext(string orderId)
        {
            return await _transactionService.GetTransactionContext<TransferContextData>(orderId);
        }
    }
}