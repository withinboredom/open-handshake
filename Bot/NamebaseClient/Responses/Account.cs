using System.Collections.Generic;
using System.Linq;

namespace Bot.NamebaseClient.Responses
{
    public class Account
    {
        public decimal MakerFee { get; set; }
        public decimal TakerFee { get; set; }
        public bool CanTrade { get; set; }
        public List<AccountBalance> Balances { get; set; }

        public AccountBalance Hns
        {
            get { return Balances.First(x => x.Asset == "HNS"); }
        }

        public AccountBalance Btc
        {
            get { return Balances.First(x => x.Asset == "BTC"); }
        }
    }
}