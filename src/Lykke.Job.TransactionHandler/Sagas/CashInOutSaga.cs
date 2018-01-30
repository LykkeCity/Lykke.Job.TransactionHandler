﻿using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class CashInOutSaga
    {
        private readonly ILog _log;
        private readonly Core.Services.BitCoin.ITransactionService _transactionService;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;

        public CashInOutSaga(
            [NotNull] ILog log,
            [NotNull] Core.Services.BitCoin.ITransactionService transactionService,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
        }

        private async Task Handle(CashoutTransactionStateSavedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(CashInOutSaga), nameof(CashoutTransactionStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            var message = evt.Message;
            var transactionId = message.Id;
            var amount = message.Amount.ParseAnyDouble();
            var clientId = message.ClientId;
            var asset = await _assetsServiceWithCache.TryGetAssetAsync(message.AssetId);
            var context = await _transactionService.GetTransactionContext<CashOutContextData>(transactionId);
            var isForwardWithdawal = context.AddData?.ForwardWithdrawal != null;
            var isSwiftCashout = context.AddData?.SwiftData != null;
            var cashOperationId = context.CashOperationId;

            if (isForwardWithdawal || isSwiftCashout)
                return;

            if (!string.IsNullOrWhiteSpace(asset.BlockchainIntegrationLayerId))
            {
                // Processes cashout using generic blockchain integration layer

                sender.SendCommand(new BlockchainCashoutProcessor.Contract.Commands.StartCashoutCommand
                {
                    OperationId = Guid.Parse(transactionId),
                    AssetId = message.AssetId,
                    Amount = (decimal)Math.Abs(amount),
                    ToAddress = context.Address,
                    ClientId = Guid.Parse(clientId)
                }, BlockchainCashoutProcessor.Contract.BlockchainCashoutProcessorBoundedContext.Name);
            }
            else if (asset.Blockchain == Blockchain.Ethereum)
            {
                sender.SendCommand(new Commands.ProcessEthereumCashoutCommand
                {
                    TransactionId = transactionId,
                    Amount = Math.Abs(amount),
                    Address = context.Address,
                    ClientId = clientId,
                    AssetId = asset.Id,
                    CashOperationId = cashOperationId
                }, BoundedContexts.Ethereum);
            }
            else if (asset.Id == LykkeConstants.SolarAssetId)
            {
                sender.SendCommand(new Commands.SolarCashOutCommand
                {
                    ClientId = clientId,
                    TransactionId = transactionId,
                    Address = context.Address,
                    Amount = Math.Abs(amount)
                }, BoundedContexts.Solarcoin);
            }
            else if (asset.Id == LykkeConstants.ChronoBankAssetId)
            {
                sender.SendCommand(new Commands.ChronoBankCashOutCommand
                {
                    TransactionId = transactionId,
                    Amount = Math.Abs(amount),
                    Address = context.Address
                }, BoundedContexts.Chronobank);
            }
            else if (asset.Blockchain == Blockchain.Bitcoin && asset.IsTrusted && asset.BlockchainWithdrawal)
            {
                sender.SendCommand(new Commands.BitcoinCashOutCommand
                {
                    TransactionId = transactionId,
                    Amount = Math.Abs(amount),
                    Address = context.Address,
                    AssetId = asset.Id
                }, BoundedContexts.Bitcoin);
            }
        }
    }
}
