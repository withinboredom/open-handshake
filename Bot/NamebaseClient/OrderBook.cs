using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bot.NamebaseClient.Responses;
using Microsoft.Extensions.Logging;

namespace Bot.NamebaseClient
{
    public class OrderBook
    {
        public List<(decimal Level, decimal Ammount)> Bids;
        public List<(decimal Level, decimal Ammount)> Asks;

        public OrderBook(DepthResponse response)
        {
            Bids = response.Bids.Select(x => (Level: decimal.Parse(x[0]), Ammount: decimal.Parse(x[1]))).Reverse().ToList();
            Asks = response.Asks.Select(x => (Level: decimal.Parse(x[0]), Ammount: decimal.Parse(x[1]))).Reverse().ToList();
        }

        public (decimal Btc, decimal ToLevel) SellHns(decimal hns)
        {
            var index = 0;
            var con = Bids;
            var btc = 0m;
            var level = con[index].Level;
            while(hns > 0)
            {
                var available = 0m;
                (level, available) = con[index++];
                var taking = Math.Min(available, hns);
                btc += taking * level;
                hns -= taking;
            }

            return (btc, level);
        }

        public (decimal Hns, decimal ToLevel) SellBtc(decimal btc)
        {
            var index = 0;
            var con = Asks;
            var hns = 0m;
            var level = con[index].Level;
            while(btc > 0)
            {
                var available = 0m;
                (level, available) = con[index++];
                var availableBtc = available * level;
                var taking = Math.Min(availableBtc, btc);
                btc -= taking;
                hns += Client.ConvertBtcToHns(taking, level);
            }

            return (hns, level);
        }
    }
}
