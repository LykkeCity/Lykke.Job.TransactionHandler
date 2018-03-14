using Lykke.Job.EthereumCore.Contracts.Enums;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Commands.EthereumCore
{
    //Event with info about EthereumCore erc20 token cashin/cashout
    [MessagePackObject(keyAsPropertyName: true)]
    public class ProcessHotWalletErc20EventCommand
    {
        public string OperationId { get; set; }
        public string TransactionHash { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public string Amount { get; set; }
        public DateTime EventTime { get; set; }
        public HotWalletEventType EventType { get; set; }
        public string TokenAddress { get; set; }
    }
}
