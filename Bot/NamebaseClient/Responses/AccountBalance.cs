namespace Bot.NamebaseClient.Responses
{
    public class AccountBalance
    {
        public string Asset { get; set; }
        public decimal Unlocked { get; set; }
        public decimal LockedInOrders { get; set; }
        public bool CanDeposit { get; set; }
        public bool CanWithdraw { get; set; }

        public decimal Total => Unlocked + LockedInOrders;
    }
}