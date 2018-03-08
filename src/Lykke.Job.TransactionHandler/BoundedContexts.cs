namespace Lykke.Job.TransactionHandler
{
    public static class BoundedContexts
    {
        public static readonly string TxHandler = "tx-handler";
        public static readonly string ForwardWithdrawal = $"{TxHandler}.forward-withdrawal";
        public static readonly string Bitcoin = $"{TxHandler}.bitcoin";
        public static readonly string Chronobank = $"{TxHandler}.chronobank";
        public static readonly string Ethereum = $"{TxHandler}.ethereum";
        public static readonly string EthereumCommands = $"{TxHandler}.ethereum.commands";
        public static readonly string Offchain = $"{TxHandler}.offchain";
        public static readonly string Solarcoin = $"{TxHandler}.solarcoin";
        public static readonly string Operations = "operations";
        public static readonly string OperationsHistory = "operations-history";
        public static readonly string Email = "email";
        public static readonly string Trades = "trades";
        public static readonly string Orders = "orders";
        public static readonly string Fee = "fee";
        public static readonly string Push = "push";
        public static readonly string Transfers = "transfers";
    }
}
