using System;
using System.Collections.Generic;
using Bot.Sensors;

namespace Bot.Rules
{
    public class DeltaRule : IRule
    {
        public List<Sources> Triggers { get; set; }
        public TradingBot.Command SuggestedBuyCommand { get; set; }
        public TradingBot.Command SuggestedSellCommand { get; set; }
        public int Priority { get; set; }
        public TimeSpan Delay { get; set; }

        public void Update()
        {
            throw new System.NotImplementedException();
        }
    }
}