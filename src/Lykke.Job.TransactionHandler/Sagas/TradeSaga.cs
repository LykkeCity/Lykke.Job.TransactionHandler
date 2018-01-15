using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Cqrs;
using Lykke.Job.TransactionHandler.Core.Services.BitCoin;
using Lykke.Job.TransactionHandler.Commands;
using Lykke.Job.TransactionHandler.Commands.Bitcoin;
using Lykke.Job.TransactionHandler.Commands.Ethereum;
using Lykke.Job.TransactionHandler.Events;
using Lykke.Job.TransactionHandler.Events.Bitcoin;
using Lykke.Job.TransactionHandler.Events.Ethereum;
using Lykke.Job.TransactionHandler.Queues.Models;
using Lykke.Service.Assets.Client.Models;

namespace Lykke.Job.TransactionHandler.Sagas
{

    public class TradeSaga
    {
        private readonly ITransactionService _transactionService;
        private readonly ILog _log;

        public TradeSaga(
            ITransactionService transactionService,
            ILog log)
        {
            _transactionService = transactionService;
            _log = log;
        }

        private async Task Handle(TradeCreatedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(TradeSaga), nameof(TradeCreatedEvent), evt.ToJson());

            if (evt.IsTrustedClient)
            {
                return;
            }

            var cmd = new CreateTransactionCommand
            {
                OrderId = evt.OrderId,
            };

            sender.SendCommand(cmd, "tx-handler");
        }

        private async Task Handle(TransactionCreatedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(TradeSaga), nameof(TransactionCreatedEvent), evt.ToJson());

            var context = await GetContext(evt.OrderId);

            if (!context.SellTransfer.Asset.IsTrusted)
            {
                switch (context.SellTransfer.Asset.Blockchain)
                {
                    case Blockchain.Ethereum:
                        {
                            var cmd = new EthGuaranteeTransferCommand
                            {
                                OrderId = evt.OrderId,
                                ClientId = context.Order.ClientId,
                                Asset = context.SellTransfer.Asset,
                                Amount = context.SellTransfer.Amount
                            };
                            sender.SendCommand(cmd, "ethereum");

                            break;
                        }
                    case Blockchain.Bitcoin:
                        {
                            var cmd = new ReturnCommand
                            {
                                TransactionId = context.SellTransfer.TransferId,
                                OrderId = evt.OrderId,
                                ClientId = context.Order.ClientId,
                                AssetId = context.SellTransfer.Asset.Id,
                                Amount = context.SellTransfer.Amount
                            };
                            sender.SendCommand(cmd, "offchain");

                            break;
                        }
                }
            }

            if (!context.BuyTransfer.Asset.IsTrusted)
            {
                switch (context.BuyTransfer.Asset.Blockchain)
                {
                    case Blockchain.Ethereum:
                        {
                            var clientId = context.Order.ClientId;

                            var cmd = new EthCreateTransactionRequestCommand
                            {
                                Id = context.BuyTransfer.TransferId,
                                OrderId = evt.OrderId,
                                ClientId = clientId,
                                AssetId = context.BuyTransfer.Asset.Id,
                                Amount = context.BuyTransfer.Amount,
                                OperationIds = context.ClientTrades.Where(x => x.ClientId == clientId && x.Amount > 0)
                                    .Select(x => x.Id)
                                    .ToArray()
                            };
                            sender.SendCommand(cmd, "ethereum");

                            break;
                        }
                    case Blockchain.Bitcoin:
                        {
                            var cmd = new TransferFromHubCommand
                            {
                                TransactionId = context.BuyTransfer.TransferId,
                                OrderId = evt.OrderId,
                                ClientId = context.Order.ClientId,
                                AssetId = context.BuyTransfer.Asset.Id,
                                Amount = context.BuyTransfer.Amount
                            };
                            sender.SendCommand(cmd, "offchain");

                            break;
                        }
                }
            }
        }

        private async Task Handle(EthTransactionRequestCreatedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(TradeSaga), nameof(EthTransactionRequestCreatedEvent), evt.ToJson());

            var cmd = new EthBuyCommand
            {
                TransactionId = evt.TransactionId,
                OrderId = evt.OrderId,
                ClientId = evt.ClientId,
                AssetId = evt.AssetId,
                Amount = evt.Amount
            };

            sender.SendCommand(cmd, "ethereum");
        }

        private async Task Handle(OffchainRequestCreatedEvent evt, ICommandSender sender)
        {
            await _log.WriteInfoAsync(nameof(TradeSaga), nameof(OffchainRequestCreatedEvent), evt.ToJson());

            var offchainNotifyCommand = new OffchainNotifyCommand
            {
                ClientId = evt.ClientId
            };

            sender.SendCommand(offchainNotifyCommand, "notifications");
        }

        private async Task<SwapOffchainContextData> GetContext(string orderId)
        {
            return await _transactionService.GetTransactionContext<SwapOffchainContextData>(orderId);
        }
    }
}