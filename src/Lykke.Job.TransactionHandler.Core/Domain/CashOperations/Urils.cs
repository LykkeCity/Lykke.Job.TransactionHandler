using System;

namespace Lykke.Job.TransactionHandler.Core.Domain.CashOperations
{
    public static class Utils
    {
        // Supports old records previously created by ME
        public static string GenerateRecordId(DateTime dt)
        {
            return $"{dt.Year}{dt.Month.ToString("00")}{dt.Day.ToString("00")}{dt.Hour.ToString("00")}{dt.Minute.ToString("00")}_{Guid.NewGuid()}";
        }
    }
}