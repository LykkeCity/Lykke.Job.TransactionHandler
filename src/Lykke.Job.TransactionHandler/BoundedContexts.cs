namespace Lykke.Job.TransactionHandler
{
    public static class BoundedContexts
    {
        public static readonly string Self = "tx-handler";
        public static readonly string ForwardWithdrawal = $"{Self}.forward-withdrawal";
        public static readonly string Bitcoin = $"{Self}.bitcoin";
        public static readonly string Chronobank = $"{Self}.chronobank";
        public static readonly string Ethereum = $"{Self}.ethereum";
        public static readonly string Offchain = $"{Self}.offchain";
        public static readonly string Solarcoin = $"{Self}.solarcoin";
        public static readonly string Operations = "operations";
        public static readonly string OperationsHistory = "operations-history";
        public static readonly string Email = "email";
        public static readonly string Trades = "trades";
        public static readonly string Orders = "orders";
        public static readonly string Fee = "fee";
        public static readonly string Push = "push";
    }
}
