using System;
using System.Threading.Tasks;
using Common;
using Lykke.Bitcoin.Api.Client.BitcoinApi;
using Lykke.Bitcoin.Api.Client.BitcoinApi.Models;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.JobTriggers.Triggers.Attributes;

namespace Lykke.Job.TransactionHandler.TriggerHandlers
{
    public class InTransactionsQueueFunctions
    {
        private readonly IBitcoinApiClient _bitcoinApiClient;
        private readonly IBitCoinTransactionsRepository _bitCoinTransactionsRepository;
        private readonly IBitcoinTransactionService _bitcoinTransactionService;

        public InTransactionsQueueFunctions(IBitcoinApiClient bitcoinApiClient,
            IBitCoinTransactionsRepository bitCoinTransactionsRepository, IBitcoinTransactionService bitcoinTransactionService)
        {
            _bitcoinApiClient = bitcoinApiClient;
            _bitCoinTransactionsRepository = bitCoinTransactionsRepository;
            _bitcoinTransactionService = bitcoinTransactionService;
        }

        [QueueTrigger("intransactions", maxPollingIntervalMs: 100, maxDequeueCount: 1)]
        public async Task ProcessInMessage(string message)
        {
            var baseCommand = message.DeserializeJson<BaseCommand>();

            switch (baseCommand.Type)
            {
                case CommandType.Unknown:
                    break;
                case CommandType.Issue:
                    await ProcessIssue(message.DeserializeJson<IssueCommand>());
                    break;
                case CommandType.CashOut:
                    await ProcessCashOut(message.DeserializeJson<CashOutCommand>());
                    break;
                case CommandType.Transfer:
                    await ProcessTransfer(message.DeserializeJson<TransferCommand>());
                    break;
                case CommandType.TransferAll:
                    await ProcessTransferAll(message.DeserializeJson<TransferAllCommand>());
                    break;
                case CommandType.Destroy:
                    await ProcessDestroyAsync(message.DeserializeJson<DestroyCommand>());
                    break;
                case CommandType.Swap:
                    await ProcessSwapAsync(message.DeserializeJson<SwapCommand>());
                    break;
            }
        }

        private async Task ProcessSwapAsync(SwapCommand cmd)
        {
            var request = new SwapData
            {
                Amount1 = (decimal)cmd.Amount1,
                Amount2 = (decimal)cmd.Amount2,
                AssetId1 = cmd.Asset1,
                AssetId2 = cmd.Asset2,
                Multisig1 = cmd.MultisigCustomer1,
                Multisig2 = cmd.MultisigCustomer2,
                TransactionId = cmd.TransactionId
            };

            var response = await _bitcoinApiClient.SwapAsyncTransaction(request);

            var reqMsg = $"{BitCoinCommands.Swap}:{request.ToJson()}";

            await ProcessBitcoinApiResponse(reqMsg, BitCoinCommands.Swap, response, null);
        }

        private async Task ProcessDestroyAsync(DestroyCommand cmd)
        {
            var request = new DestroyData
            {
                Address = cmd.Address,
                AssetId = cmd.AssetId,
                Amount = (decimal)cmd.Amount,
                TransactionId = cmd.TransactionId
            };

            var response = await _bitcoinApiClient.DestroyAsync(request);

            var reqMsg = $"{BitCoinCommands.Destroy}:{request.ToJson()}";

            await ProcessBitcoinApiResponse(reqMsg, BitCoinCommands.Destroy, response, cmd.Context);
        }

        private async Task ProcessTransferAll(TransferAllCommand cmd)
        {
            var request = new TransferAllData
            {
                DestinationAddress = cmd.DestinationAddress,
                SourceAddress = cmd.SourceAddress,
                TransactionId = cmd.TransactionId
            };

            var response = await _bitcoinApiClient.TransferAllAsync(request);
            var reqMsg = $"{BitCoinCommands.TransferAll}:{request.ToJson()}";

            if (response.Error != null && response.Error.ErrorCode == ErrorCode.NoCoinsFound) //skip transferall error for empty wallet
                return;

            await ProcessBitcoinApiResponse(reqMsg, BitCoinCommands.TransferAll, response, cmd.Context);
        }

        private async Task ProcessTransfer(TransferCommand cmd)
        {
            var request = new TransferData
            {
                TransactionId = cmd.TransactionId,
                SourceAddress = cmd.SourceAddress,
                DestinationAddress = cmd.DestinationAddress,
                Amount = (decimal)cmd.Amount,
                AssetId = cmd.AssetId
            };

            var response = await _bitcoinApiClient.TransferAsync(request);
            var reqMsg = $"{BitCoinCommands.Transfer}:{request.ToJson()}";

            await ProcessBitcoinApiResponse(reqMsg, BitCoinCommands.Transfer, response, cmd.Context);
        }

        private async Task ProcessCashOut(CashOutCommand cmd)
        {
            var request = new TransferData
            {
                SourceAddress = cmd.SourceAddress,
                DestinationAddress = cmd.DestinationAddress,
                Amount = (decimal)cmd.Amount,
                AssetId = cmd.AssetId,
                TransactionId = cmd.TransactionId
            };

            var response = await _bitcoinApiClient.TransferAsync(request);
            var reqMsg = $"{BitCoinCommands.CashOut}:{request.ToJson()}";

            await ProcessBitcoinApiResponse(reqMsg, BitCoinCommands.CashOut, response, cmd.Context);
        }

        private async Task ProcessIssue(IssueCommand cmd)
        {
            var request = new IssueData
            {
                TransactionId = cmd.TransactionId,
                Address = cmd.Multisig,
                Amount = (decimal)cmd.Amount,
                AssetId = cmd.AssetId
            };

            var response = await _bitcoinApiClient.IssueAsync(request);
            var reqMsg = $"{BitCoinCommands.Issue}:{request.ToJson()}";

            await ProcessBitcoinApiResponse(reqMsg, BitCoinCommands.Issue, response, cmd.Context);
        }

        private async Task<TransactionResponse> ProcessBitcoinApiResponse(string request, string commandType, OnchainResponse response, string contextData, Guid? existingTxId = null)
        {
            var id = Guid.NewGuid().ToString();

            if (response.Error != null)
            {
                await _bitCoinTransactionsRepository.CreateAsync(id, commandType, request, null, response?.ToJson());
            }
            else
            {
                id = response.Transaction.TransactionId.ToString();
                await _bitCoinTransactionsRepository.UpdateAsync(id, request, null, response?.ToJson());
            }

            await _bitcoinTransactionService.SetStringTransactionContext(id, contextData);
            
            return response.Transaction;
        }
    }
}
