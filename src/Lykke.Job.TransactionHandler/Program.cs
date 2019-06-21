﻿using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Sdk;

namespace Lykke.Job.TransactionHandler
{
    [UsedImplicitly]
    internal sealed class Program
    {
        public static async Task Main(string[] args)
        {
#if DEBUG
            await LykkeStarter.Start<Startup>(true);
#else
            await LykkeStarter.Start<Startup>(false);
#endif
        }
    }
}
