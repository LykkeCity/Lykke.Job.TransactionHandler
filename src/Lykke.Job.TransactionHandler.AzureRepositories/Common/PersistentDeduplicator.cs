using System;
using JetBrains.Annotations;
using Lykke.Job.TransactionHandler.Core.Domain.Common;
using Lykke.Job.TransactionHandler.Core.Services;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Common
{
    public class PersistentDeduplicator : IDeduplicator
    {
        private const int ConflictStatusCode = 409;
        private readonly IBlobRepository _blobRepository;

        public PersistentDeduplicator([NotNull] IBlobRepository blobRepository)
        {
            _blobRepository = blobRepository ?? throw new ArgumentNullException(nameof(blobRepository));
        }

        public bool EnsureNotDuplicate(object value)
        {
            try
            {
                var key = _blobRepository.Insert(value).Result;
                return true;
            }
            catch (Microsoft.WindowsAzure.Storage.StorageException exception)
            {
                if (exception.RequestInformation.HttpStatusCode != ConflictStatusCode)
                    throw;
            }
            return false;
        }
    }
}
