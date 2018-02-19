using System.Threading.Tasks;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;

namespace Lykke.Job.TransactionHandler.Core.Domain.SolarCoin
{
    public interface ISrvSolarCoinCommandProducer
    {
        Task ProduceCashOutCommand(string id, SolarCoinAddress addressTo, decimal amount);
    }
}