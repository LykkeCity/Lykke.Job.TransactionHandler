using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Logs
{
    public interface ICashInOutLog
    {
        string TransactionId { get; }
        string ClientId { get; }
        DateTime DateTime { get; }
        string Volume { get; }
        string Asset { get; }
        string FeeInstructions { get; }
        string FeeTransfers { get; }
    }

    public interface ICashInOutLogRepository
    {
        Task CreateAsync(string transactionId, string clientId, DateTime dateTime, string volume, string asset, string feeInstructions, string feeTransfers);
    }
}
