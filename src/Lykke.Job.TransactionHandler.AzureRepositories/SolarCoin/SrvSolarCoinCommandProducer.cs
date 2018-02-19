using System.Threading.Tasks;
using AzureStorage.Queue;
using Common;
using Lykke.Job.TransactionHandler.Core.Domain.SolarCoin;
using Lykke.Job.TransactionHandler.Core.Services.SolarCoin;

namespace Lykke.Job.TransactionHandler.AzureRepositories.SolarCoin
{
    public class SrvSolarCoinCommandProducer : ISrvSolarCoinCommandProducer
    {
        private readonly IQueueExt _queueExt;

        public SrvSolarCoinCommandProducer(IQueueExt queueExt)
        {
            _queueExt = queueExt;
        }

        public async Task ProduceCashOutCommand(string id, SolarCoinAddress addressTo, decimal amount)
        {
            await _queueExt.PutRawMessageAsync(new SolarCashOutCommand
            {
                Id = id,
                Amount = amount,
                Address = addressTo.Value
            }.ToJson());
        }

        public class SolarCashOutCommand
        {
            public string Id { get; set; }
            public string Address { get; set; }
            public decimal Amount { get; set; }
        }
    }
}