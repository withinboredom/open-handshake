using System;
using Bot.NamebaseClient.Responses;

namespace Bot.Charting
{
    public static partial class ChartGraphics
    {
        public struct OrderFill
        {
            private decimal _level;
            private DateTime _time;

            public DateTime Time
            {
                get => _time;
                set => _time = value.ToUniversalTime();
            }

            public decimal Level
            {
                get => _level;
                set => _level = value * 1000000000000m;
            }

            public bool IsFilled { get; set; }
            public OrderSide Side { get; set; }
        }
    }
}