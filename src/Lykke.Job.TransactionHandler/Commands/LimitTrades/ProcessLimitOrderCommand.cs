﻿using Lykke.Job.TransactionHandler.Queues.Models;
using MessagePack;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands.LimitTrades
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class ProcessLimitOrderCommand
    {        
        public LimitQueueItem.LimitOrderWithTrades LimitOrder { get; set; }
    }
}