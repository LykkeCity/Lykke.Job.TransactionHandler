using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Job.TransactionHandler.Core.Domain.Ethereum
{
    public interface IEthereumCashinAggregate
    {
    }

    public class EthereumCashinAggregate : IEthereumCashinAggregate
    {
        public EthereumCashinState State { get; set; }

        public EthereumCashinAggregate()
        {
        }

        //public bool OnClientOperationFinishRegisteredEvent()
        //{
        //    if (!SwitchState(CashoutState.OperationIsFinished, CashoutState.ClientOperationFinishIsRegistered))
        //    {
        //        return false;
        //    }

        //    ClientOperationFinishRegistrationMoment = DateTime.UtcNow;

        //    return true;
        //}

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
    }
}
