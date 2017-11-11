using System.Collections.Generic;
using Lykke.Job.TransactionHandler.Core.Domain.Health;
using Lykke.Job.TransactionHandler.Core.Services;

namespace Lykke.Job.TransactionHandler.Services
{
    public class HealthService : IHealthService
    {
        public string GetHealthViolationMessage()
        {
            // TODO: Check gathered health statistics, and return appropriate health violation message, or NULL if job hasn't critical errors
            return null;
        }

        public IEnumerable<HealthIssue> GetHealthIssues()
        {
            var issues = new HealthIssuesCollection();

            // TODO: Check gathered health statistics, and add appropriate health issues message to issues

            return issues;
        }
    }
}