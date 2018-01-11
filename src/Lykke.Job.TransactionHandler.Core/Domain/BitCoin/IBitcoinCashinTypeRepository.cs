using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.BitCoin
{
    public interface IBitcoinCashin
    {
        string Id { get; }
        string ClientId { get; set; }
        string Address { get; set; }
        string TxHash { get; set; }
        bool IsSegwit { get; set; }
    }

    public interface IBitcoinCashinRepository
    {
        Task<IBitcoinCashin> GetAsync(string id);
    }
}
