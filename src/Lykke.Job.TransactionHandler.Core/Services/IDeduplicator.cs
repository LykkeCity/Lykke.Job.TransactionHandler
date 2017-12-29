namespace Lykke.Job.TransactionHandler.Core.Services
{
    public interface IDeduplicator
    {
        bool EnsureNotDuplicate(object key);
    }
}