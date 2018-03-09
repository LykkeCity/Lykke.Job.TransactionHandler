using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Core.Domain.Ethereum
{
    public class EthereumCashinAggregate
    {
        public string TransactionHash { get; private set; }
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public string ClientAddress { get; set; }
        public decimal Amount { get; set; }
        public bool CreatePendingActions { get; set; }
        public Guid CashinOperationId { get; private set; }
        public EthereumCashinState State { get; private set; }
        public string Version { get; set; }

        public DateTime? CashinEnrolledToMatchingEngineDate { get; private set; }
        public DateTime? HistorySavedDate { get; private set; }

        public EthereumCashinAggregate(string transactionHash,
            string clientId,
            string assetId,
            string clientAddress,
            decimal amount,
            bool createPendingActions)
        {
            TransactionHash = transactionHash;
            ClientId = clientId;
            AssetId = assetId;
            ClientAddress = clientAddress;
            Amount = amount;
            CreatePendingActions = createPendingActions;
            CashinOperationId = Guid.NewGuid();
        }

        private EthereumCashinAggregate(string version,
            EthereumCashinState state,
            string transactionHash,
            string clientId,
            string assetId,
            string clientAddress,
            decimal amount,
            bool createPendingActions,
            Guid cashinOperationId,
            DateTime? cashinEnrolledToMatchingEngineDate,
            DateTime? historySavedDate)
        {
            Version = version;
            State = state;
            TransactionHash = transactionHash;
            ClientId = clientId;
            AssetId = assetId;
            ClientAddress = clientAddress;
            Amount = amount;
            CreatePendingActions = createPendingActions;
            CashinOperationId = cashinOperationId;
            CashinEnrolledToMatchingEngineDate = cashinEnrolledToMatchingEngineDate;
            HistorySavedDate = historySavedDate;
        }

        public bool OnEnrolledToMatchingEngineEvent()
        {
            if (!SwitchState(EthereumCashinState.CashinStarted, EthereumCashinState.CashinEnrolledToMatchingEngine))
            {
                return false;
            }

            CashinEnrolledToMatchingEngineDate = DateTime.UtcNow;

            return true;
        }

        public bool OnHistorySavedEvent()
        {
            if (!SwitchState(EthereumCashinState.CashinEnrolledToMatchingEngine, EthereumCashinState.CashinCompleted))
            {
                return false;
            }

            HistorySavedDate = DateTime.UtcNow;

            return true;
        }

        private bool SwitchState(EthereumCashinState expectedState, EthereumCashinState nextState)
        {
            if (State < expectedState)
            {
                // Throws to retry and wait until aggregate will be in the required state
                throw new InvalidAggregateStateException(State, expectedState, nextState);
            }

            if (State > expectedState)
            {
                // Aggregate already in the next state, so this event can be just ignored
                return false;
            }

            State = nextState;

            return true;
        }

        public static EthereumCashinAggregate Restore(
            string version,
            EthereumCashinState state,
            string transactionHash,
            string clientId, string assetId,
            string clientAddress,
            decimal amount,
            bool createPendingActions,
            Guid cashinOperationId,
            DateTime? cashinEnrolledToMatchingEngineDate,
            DateTime? historySavedDate)
        {
            return new EthereumCashinAggregate(version,
                state,
                transactionHash,
                clientId,
                assetId,
                clientAddress,
                amount,
                createPendingActions,
                cashinOperationId,
                cashinEnrolledToMatchingEngineDate,
                historySavedDate);
        }
    }
}
