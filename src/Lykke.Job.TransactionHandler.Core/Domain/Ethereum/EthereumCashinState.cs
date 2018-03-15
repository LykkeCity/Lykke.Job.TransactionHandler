using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Core.Domain.Ethereum
{
    public enum EthereumCashinState
    {
        CashinStarted,
        CashinEnrolledToMatchingEngine,
        CashinCompleted
    }
}
