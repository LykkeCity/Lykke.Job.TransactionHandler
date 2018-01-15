using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Offchain
{
    public interface IOrder
    {
        string Id { get; }
        string OrderId { get; }
        string ClientId { get; set; }
        DateTime CreatedAt { get; set; }
        decimal Volume { get; set; }
        decimal ReservedVolume { get; set; }
        string AssetPair { get; set; }
        string Asset { get; set; }
        bool Straight { get; set; }
        decimal Price { get; set; }
        bool IsLimit { get; set; }
    }

    public interface IOrdersRepository
    {
        Task<IOrder> GetOrder(string id);
    }
}