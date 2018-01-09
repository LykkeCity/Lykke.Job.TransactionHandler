using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core;
using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Utils;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;

namespace Lykke.Job.TransactionHandler.Sagas
{
    public class CashInOutSaga
    {
        private readonly ILog _log;
        private readonly Core.Services.BitCoin.IBitcoinTransactionService _bitcoinTransactionService;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;

        public CashInOutSaga(
            [NotNull] ILog log,
            [NotNull] Core.Services.BitCoin.IBitcoinTransactionService bitcoinTransactionService,
            [NotNull] IAssetsServiceWithCache assetsServiceWithCache)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _bitcoinTransactionService = bitcoinTransactionService ?? throw new ArgumentNullException(nameof(bitcoinTransactionService));
            _assetsServiceWithCache = assetsServiceWithCache ?? throw new ArgumentNullException(nameof(assetsServiceWithCache));
        }
        
        private async Task Handle(DestroyTransactionStateSavedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(CashInOutSaga), nameof(DestroyTransactionStateSavedEvent), evt.ToJson(), "");

            ChaosKitty.Meow();

            sender.SendCommand(new Commands.SendBitcoinCommand
            {
                Command = evt.Command
            }, "bitcoin");
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
            var context = await _bitcoinTransactionService.GetTransactionContext<CashOutContextData>(transactionId);
            var isForwardWithdawal = context.AddData?.ForwardWithdrawal != null;
            var cashOperationId = context.CashOperationId;

            if (isForwardWithdawal)
            {
                return;
            }

            if (asset.Blockchain == Blockchain.Ethereum)
            {
                sender.SendCommand(new Commands.ProcessEthereumCashoutCommand
                {
                    TransactionId = transactionId,
                    Amount = Math.Abs(amount),
                    Address = context.Address,
                    ClientId = clientId,
                    AssetId = asset.Id,
                    CashOperationId = cashOperationId
                }, "ethereum");
            }
            else if (asset.Id == LykkeConstants.SolarAssetId)
            {
                sender.SendCommand(new Commands.SolarCashOutCommand
                {
                    ClientId = clientId,
                    TransactionId = transactionId,
                    Address = context.Address,
                    Amount = Math.Abs(amount)
                }, "solarcoin");
            }
            else if (asset.Id == LykkeConstants.ChronoBankAssetId)
            {
                sender.SendCommand(new Commands.ChronoBankCashOutCommand
                {
                    TransactionId = transactionId,
                    Amount = Math.Abs(amount),
                    Address = context.Address
                }, "chronobank");
            }
            else if (asset.Blockchain == Blockchain.Bitcoin && asset.IsTrusted && asset.BlockchainWithdrawal)
            {
                sender.SendCommand(new Commands.BitcoinCashOutCommand
                {
                    TransactionId = transactionId,
                    Amount = Math.Abs(amount),
                    Address = context.Address,
                    AssetId = asset.Id
                }, "bitcoin");
            }
        }
    }
}
