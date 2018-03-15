using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Core.Domain.Ethereum
{
    public class InvalidAggregateStateException : Exception
    {
        public InvalidAggregateStateException(object currentState, object expectedState, object targetState) :
            base(BuildMessage(currentState, expectedState, targetState))
        {

        }

        private static string BuildMessage(object currentState, object expectedState, object targetState)
        {
            return $"State can't be switched: {currentState} -> {targetState}. Waiting for the {expectedState} state.";
        }
    }
}
