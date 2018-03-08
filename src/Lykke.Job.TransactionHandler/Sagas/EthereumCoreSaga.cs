using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Core.Domain.PaymentSystems;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Events.EthereumCore;
using Lykke.Service.Assets.Client;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class EthereumCoreSaga
    {
        private readonly ILog _log;
        private readonly IEthereumCashinAggregateRepository _ethereumCashinAggregateRepository;
        private readonly ITransactionService _transactionService;
        private readonly IAssetsService _assetsService;
        private readonly IEthererumPendingActionsRepository _ethererumPendingActionsRepository;
        private readonly IPaymentTransactionsRepository _paymentTransactionsRepository;

        public EthereumCoreSaga(
            [NotNull] ILog log,
            [NotNull] IEthereumCashinAggregateRepository ethereumCashinAggregateRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _ethereumCashinAggregateRepository = ethereumCashinAggregateRepository;
        }

        private async Task Handle(StartCashinEvent evt, ICommandSender sender)
        {
            _ethereumCashinAggregateRepository.s
            var id = Guid.NewGuid().ToString("N");
        }
    }
}