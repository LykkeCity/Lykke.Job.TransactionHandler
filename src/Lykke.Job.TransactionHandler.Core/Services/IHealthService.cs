using System.Collections.Generic;
using Lykke.Job.TransactionHandler.Core.Domain.Health;

namespace Lykke.Job.TransactionHandler.Core.Services
{
    public interface IHealthService
    {
        string GetHealthViolationMessage();
        IEnumerable<HealthIssue> GetHealthIssues();
    }
}