using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.OperationsRepository.AutorestClient.Models;
using Lykke.Service.OperationsRepository.Client.Abstractions.CashOperations;

namespace Lykke.Job.TransactionHandler.Handlers
{
    public class OperationsCommandHandler
    {
        private readonly ILog _log;
        private readonly ICashOperationsRepositoryClient _cashOperationsRepositoryClient;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;
        private readonly IWalletCredentialsRepository _walletCredentialsRepository;
        private readonly IBitCoinTransactionsRepository _bitcoinTransactionsRepository;

        public OperationsCommandHandler(
            [NotNull] ILog log,
            [NotNull] ICashOperationsRepositoryClient cashOperationsRepositoryClient,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache,
            [NotNull] IWalletCredentialsRepository walletCredentialsRepository,
            [NotNull] IBitCoinTransactionsRepository bitcoinTransactionsRepository)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cashOperationsRepositoryClient = cashOperationsRepositoryClient ?? throw new ArgumentNullException(nameof(cashOperationsRepositoryClient));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
            _walletCredentialsRepository = walletCredentialsRepository ?? throw new ArgumentNullException(nameof(walletCredentialsRepository));
            _bitcoinTransactionsRepository = bitcoinTransactionsRepository ?? throw new ArgumentNullException(nameof(bitcoinTransactionsRepository));
        }

        public async Task<CommandHandlingResult> Handle(Commands.RegisterCashInOutOperationCommand command)
        {
            await _log.WriteInfoAsync(nameof(OperationsCommandHandler), nameof(Commands.RegisterCashInOutOperationCommand), command.ToJson(), "");

            ChaosKitty.Meow();

            var message = command.Message;
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(message.AssetId);
            var walletCredentials = await _walletCredentialsRepository.GetAsync(message.ClientId);
            var isBtcOffchainClient = asset.Blockchain == Blockchain.Bitcoin;

            var transaction = await _bitcoinTransactionsRepository.FindByTransactionIdAsync(message.Id);
            var context = transaction.GetContextData<CashOutContextData>();
            var operation = new CashInOutOperation
            {
                Id = transaction.TransactionId,
                ClientId = message.ClientId,
                Multisig = walletCredentials.MultiSig,
                AssetId = message.AssetId,
                Amount = message.Amount.ParseAnyDouble(),
                DateTime = DateTime.UtcNow,
                AddressFrom = walletCredentials.MultiSig,
                AddressTo = context.Address,
                TransactionId = transaction.TransactionId,
                Type = CashOperationType.None,
                BlockChainHash = asset.IssueAllowed && isBtcOffchainClient ? string.Empty : transaction.BlockchainHash,
                State = GetTransactionState(transaction.BlockchainHash, isBtcOffchainClient)
            };

            operation.AddFeeDataToOperation(message, _log);

            await RegisterOperation(operation);

            return CommandHandlingResult.Ok();
        }

        private static TransactionStates GetTransactionState(string blockchainHash, bool isBtcOffchainClient)
        {
            return isBtcOffchainClient
                ? (string.IsNullOrWhiteSpace(blockchainHash)
                    ? TransactionStates.SettledOffchain
                    : TransactionStates.SettledOnchain)
                : (string.IsNullOrWhiteSpace(blockchainHash)
                    ? TransactionStates.InProcessOnchain
                    : TransactionStates.SettledOnchain);
        }

        private async Task RegisterOperation(CashInOutOperation operation)
        {
            var operationId = await _cashOperationsRepositoryClient.RegisterAsync(operation);
            if (operationId != operation.Id)
            {
                await _log.WriteWarningAsync(nameof(OperationsCommandHandler),
                    nameof(RegisterOperation), operation.ToJson(),
                    $"Unexpected response from Operations Service: {operationId}");
            }
        }
    }
}