using AzureStorage;
using Lykke.Job.TransactionHandler.Core.Domain.Clients;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Clients
{
    public class ClientCommentEntity : TableEntity, IClientComment
    {
        public static string GeneratePartitionKey(string clientId)
        {
            return clientId;
        }

        public static string GenerateRowKey(string id)
        {
            return id;
        }

        public string Id => RowKey;
        public string ClientId { get; set; }
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }

        public static ClientCommentEntity Create(IClientComment src)
        {
            return new ClientCommentEntity
            {
                PartitionKey = GeneratePartitionKey(src.ClientId),
                RowKey = GenerateRowKey(Guid.NewGuid().ToString()),
                ClientId = src.ClientId,
                Comment = src.Comment,
                FullName = src.FullName,
                UserId = src.UserId,
                CreatedAt = src.CreatedAt
            };
        }
    }

    public class ClientCommentsRepository : IClientCommentsRepository
    {
        private readonly INoSQLTableStorage<ClientCommentEntity> _table;

        public ClientCommentsRepository(INoSQLTableStorage<ClientCommentEntity> table)
        {
            _table = table;
        }

        public async Task<IEnumerable<IClientComment>> GetClientCommentsAsync(string clientId)
        {
            return (await _table.GetDataAsync(ClientCommentEntity.GeneratePartitionKey(clientId))).OrderBy(x => x.CreatedAt);
        }

        public Task AddClientCommentAsync(IClientComment data)
        {
            return _table.InsertOrReplaceAsync(ClientCommentEntity.Create(data));
        }
    }
}
