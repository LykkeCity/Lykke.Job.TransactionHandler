using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.Common;
using Lykke.Job.TransactionHandler.Core.Services;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Common
{
    public class PersistentDeduplicator : IDeduplicator
    {
        private readonly IBlobRepository _blobRepository;

        public PersistentDeduplicator([NotNull] IBlobRepository blobRepository)
        {
            _blobRepository = blobRepository ?? throw new ArgumentNullException(nameof(blobRepository));
        }

        public async Task<bool> EnsureNotDuplicateAsync(object value)
        {
            try
            {
                await _blobRepository.Insert(value);
                return true;
            }
            catch (Microsoft.WindowsAzure.Storage.StorageException exception)
            {
                if (exception.RequestInformation.HttpStatusCode != AzureHelper.ConflictStatusCode)
                    throw;
            }
            return false;
        }
    }
}
