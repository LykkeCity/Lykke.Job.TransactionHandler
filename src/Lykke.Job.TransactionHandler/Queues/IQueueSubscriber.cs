using System;

namespace Lykke.Job.TransactionHandler.Queues
{
    public interface IQueueSubscriber : IDisposable
    {
        void Start();
        void Stop();
    }
}
