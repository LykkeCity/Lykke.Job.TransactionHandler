using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Services.Offchain
{
    public interface INotificationsService
    {
        Task OffchainNotifyUser(string clientId);
    }
}