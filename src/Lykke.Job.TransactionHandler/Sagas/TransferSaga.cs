using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Events;
using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class TransferSaga
    {
        private readonly IEthereumTransactionRequestRepository _ethereumTransactionRequestRepository;

        public TransferSaga(
            [NotNull] IEthereumTransactionRequestRepository ethereumTransactionRequestRepository)
        {
            _ethereumTransactionRequestRepository = ethereumTransactionRequestRepository ?? throw new ArgumentNullException(nameof(ethereumTransactionRequestRepository));
        }

        public async Task Handle(TransferOperationStateSavedEvent evt, ICommandSender sender)
        {
            var transactionId = evt.TransactionId;

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
