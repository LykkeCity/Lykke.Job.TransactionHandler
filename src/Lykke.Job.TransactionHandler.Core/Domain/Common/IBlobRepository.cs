using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Common
{
    public interface IBlobRepository
    {
        Task<string> Insert(object value);
    }
}
