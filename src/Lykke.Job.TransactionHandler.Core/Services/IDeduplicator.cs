using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Services
{
    public interface IDeduplicator
    {
        Task<bool> EnsureNotDuplicateAsync(object value);
    }
}
