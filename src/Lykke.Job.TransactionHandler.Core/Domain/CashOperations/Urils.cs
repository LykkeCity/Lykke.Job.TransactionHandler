using System;

namespace Lykke.Job.TransactionHandler.Core.Domain.CashOperations
{
    public static class Utils
    {
        // Supports old records previously created by ME
        public static string GenerateRecordId(DateTime dt)
        {
            return $"{dt.Year}{dt.Month:00}{dt.Day:00}{dt.Hour:00}{dt.Minute:00}_{Guid.NewGuid()}";
        }
    }
}