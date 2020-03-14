using System;
using System.Collections.Generic;
using Bot.Sensors;

namespace Bot.Rules
{
    public interface IRule
    {
        List<Sources> Triggers { get; set; }
        TradingBot.Command SuggestedBuyCommand { get; set; }
        TradingBot.Command SuggestedSellCommand { get; set; }
        int Priority { get; set; }
        TimeSpan Delay { get; set; }

        void Update();
    }
}