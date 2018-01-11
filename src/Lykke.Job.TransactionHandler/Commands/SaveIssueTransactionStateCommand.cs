﻿using Lykke.Job.TransactionHandler.Core.Domain.BitCoin;
using Lykke.Job.TransactionHandler.Queues.Models;
using ProtoBuf;

namespace Lykke.Job.TransactionHandler.Commands
{
    [ProtoContract]
    public class SaveIssueTransactionStateCommand
    {
        [ProtoMember(1)]
        public CashInOutQueueMessage Message { get; set; }

        [ProtoMember(2)]
        public IssueCommand Command { get; set; }

        [ProtoMember(3)]
        public IssueContextData Context { get; set; }
    }
}
