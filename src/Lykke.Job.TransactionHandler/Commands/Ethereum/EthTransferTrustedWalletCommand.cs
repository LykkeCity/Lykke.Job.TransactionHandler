using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;

namespace Lykke.Job.TransactionHandler.Commands.Ethereum
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class EthTransferTrustedWalletCommand
    {
        public IEthereumTransactionRequest TxRequest { get; set; }
        
        public TransferType TransferType { get; set; }
    }
}
