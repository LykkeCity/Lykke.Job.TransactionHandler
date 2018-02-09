namespace Lykke.Job.TransactionHandler.Commands.LimitTrades
{
    public class UpdateLimitOrdersCountCommand
    {
        public string ClientId { get; set; }

        public bool IsTrustedClient { get; set; }
    }
}