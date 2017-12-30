namespace Lykke.Job.TransactionHandler.Commands
{
    public class CreateOffchainCashoutRequestCommand
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }
    }
}