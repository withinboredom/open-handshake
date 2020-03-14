using System;

namespace Bot.Charting
{
    public static partial class ChartGraphics
    {
        public struct Point
        {
            public DateTime Time { get; set; }
            public decimal Value { get; set; }
        }
    }
}