using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.ChronoBank
{
    public interface IChronoBankCommandProducer
    {
        Task ProduceCashOutCommand(string id, string addressTo, decimal amount);
    }
}