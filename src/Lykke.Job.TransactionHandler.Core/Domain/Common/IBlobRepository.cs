using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Common
{
    public interface IBlobRepository
    {
        Task<bool> TryInsert(object value);
    }
}
