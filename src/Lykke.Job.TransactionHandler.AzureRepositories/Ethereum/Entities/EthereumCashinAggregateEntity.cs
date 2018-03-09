using Common;
using Lykke.AzureStorage.Tables;
using Lykke.Job.TransactionHandler.Core.Domain.Ethereum;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.AzureRepositories.Ethereum.Entities
{
    public class EthereumCashinAggregateEntity : AzureTableEntity
    {
        #region Fields

        // ReSharper disable MemberCanBePrivate.Global

        public string TransactionHash { get; set; }
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public string ClientAddress { get; set; }
        public decimal Amount { get; set; }
        public bool CreatePendingActions { get; set; }
        public Guid CashinOperationId { get; set; }
        public EthereumCashinState State { get; set; }

        public DateTime? CashinEnrolledToMatchingEngineDate { get; set; }
        public DateTime? HistorySavedDate { get; set; }

        // ReSharper restore MemberCanBePrivate.Global

        #endregion


        #region Keys

        public static string GetPartitionKey(string transactionHash)
        {
            // Use hash to distribute all records to the different partitions
            var hash = transactionHash.CalculateHexHash32(3);

            return $"{hash}";
        }

        public static string GetRowKey(string transactionHash)
        {
            return transactionHash?.ToLower();
        }

        #endregion


        #region Conversion

        public static EthereumCashinAggregateEntity FromDomain(EthereumCashinAggregate aggregate)
        {
            return new EthereumCashinAggregateEntity
            {
                ETag = string.IsNullOrEmpty(aggregate.Version) ? "*" : aggregate.Version,
                PartitionKey = GetPartitionKey(aggregate.TransactionHash),
                RowKey = GetRowKey(aggregate.TransactionHash),
                TransactionHash = aggregate.TransactionHash,
                ClientId = aggregate.ClientId,
                AssetId = aggregate.AssetId,
                ClientAddress = aggregate.ClientAddress,
                Amount = aggregate.Amount,
                CreatePendingActions = aggregate.CreatePendingActions,
                CashinOperationId = aggregate.CashinOperationId
            };
        }

        public EthereumCashinAggregate ToDomain()
        {
            return EthereumCashinAggregate.Restore(
                ETag,
                State,
                TransactionHash,
                ClientId,
                AssetId,
                ClientAddress,
                Amount,
                CreatePendingActions,
                CashinOperationId);
        }

        #endregion
    }
}
