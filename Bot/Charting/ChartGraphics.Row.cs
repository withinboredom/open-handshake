using System;

namespace Bot.Charting
{
    public static partial class ChartGraphics
    {
        public struct Row
        {
            private decimal _bottomCeiling;
            private decimal _topCeiling;
            private DateTime _time;

            public DateTime Time
            {
                get => _time;
                set => _time = value.ToUniversalTime();
            }

            public decimal Low { get; set; }
            public decimal High { get; set; }
            public decimal MidPoint { get; set; }
            
            public decimal Raw { get; set; }
            public decimal Signal { get; set; }

            public decimal TopCeiling
            {
                get => _topCeiling;
                set => _topCeiling = value * 1000000000000m;
            }

            public decimal BottomCeiling
            {
                get => _bottomCeiling;
                set => _bottomCeiling = value * 1000000000000m;
            }
        }
    }
}