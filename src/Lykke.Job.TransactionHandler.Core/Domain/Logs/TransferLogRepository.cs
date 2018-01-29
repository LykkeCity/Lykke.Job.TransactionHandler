using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Logs
{
    public interface ITransferLog
    {
        string TransferId { get;}
        DateTime TransferDate { get; }
        string FromClientId { get; }
        string ToClientid { get; }
        string AssetId { get;  }
        string Amount { get;  }
        string FeeInstruction { get; }
        string FeeTransfer { get; }
    }

    public interface ITransferLogRepository
    {
        Task CreateAsync(string transferId, DateTime transferDate, string fromClientId, string toClientid, string assetId, string amount, string feeInstruction, string feeTransfer);
    }
}
