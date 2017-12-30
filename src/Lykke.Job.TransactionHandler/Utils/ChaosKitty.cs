using System;

namespace Lykke.Job.TransactionHandler.Utils
{
    public static class ChaosKitty
    {
        private static readonly Random Randmom = new Random();
        private static double _stateOfChaos;

        public static double StateOfChaos
        {
            set
            {
                if (value < 0.0 || value > 1.0)
                    throw new ArgumentOutOfRangeException();

                _stateOfChaos = value;
            }
        }

        public static void Meow()
        {
            if (_stateOfChaos < double.Epsilon)
                return;

            if (Randmom.NextDouble() < _stateOfChaos)
                throw new Exception("Meow");
        }
    }
}
