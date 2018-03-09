using Lykke.Job.EthereumCore.Contracts.Enums;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Commands.EthereumCore
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class ProcessCoinEventCommand
    {
        public string OperationId { get; set; }
        public CoinEventType CoinEventType { get; set; }
        public string TransactionHash { get; set; }
        public string ContractAddress { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public string Amount { get; set; }
        public string Additional { get; set; }
        public DateTime EventTime { get; set; }
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class ProcessHotWalletEventCommand
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
