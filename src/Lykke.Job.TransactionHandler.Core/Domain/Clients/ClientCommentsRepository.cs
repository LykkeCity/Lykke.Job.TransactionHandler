using System;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Core.Domain.Clients
{
    public interface IClientComment
    {
        string Id { get; }
        string ClientId { get; }
        string UserId { get; }
        string FullName { get; }
        string Comment { get; }
        DateTime CreatedAt { get; }
    }

    public class ClientComment : IClientComment
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public interface IClientCommentsRepository
    {
        Task AddClientCommentAsync(IClientComment data);
    }
}
