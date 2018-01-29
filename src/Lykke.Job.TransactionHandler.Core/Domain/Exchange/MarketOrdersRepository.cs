using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Exchange
{
    public interface IMarketOrder : IOrderBase
    {
        DateTime? MatchedAt { get; }

        string MatchingId { get; set; }
    }

    public interface IMarketOrdersRepository
    {
        Task<bool> TryCreateAsync(IMarketOrder marketOrder);
    }
}