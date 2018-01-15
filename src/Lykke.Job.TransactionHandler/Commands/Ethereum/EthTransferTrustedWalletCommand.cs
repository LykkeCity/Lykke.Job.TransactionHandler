using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using Lykke.Job.TransactionHandler.Queues.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands.Ethereum
{
    [ProtoContract]
    public class EthTransferTrustedWalletCommand
    {
        [ProtoMember(1)]
        public IEthereumTransactionRequest TxRequest { get; set; }

        [ProtoMember(2)]
        public TransferType TransferType { get; set; }
    }
}
