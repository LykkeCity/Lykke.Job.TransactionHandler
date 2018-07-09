using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Job.TransactionHandler.Utils
{
    public static class ExecuteWithTimeoutHelper
    {
        public static async Task ExecuteWithTimeoutAsync(Func<Task> func, int timeoutMs)
        {
            var timeoutTask = Task.Delay(timeoutMs);
            var executionTask = func();

            var completedTaskIndex = Task.WaitAny(timeoutTask, executionTask);

            // o -means timeoutTask
            if (completedTaskIndex == 0)
                throw  new TimeoutException($"Operation timed out after {timeoutMs} (ms)");
        }
    }
}
