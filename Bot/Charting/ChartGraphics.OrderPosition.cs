using System;

namespace Bot.Charting
{
    public static partial class ChartGraphics
    {
        public struct OrderPosition
        {
            private decimal _level;
            private DateTime _startTime;
            private DateTime _endTime;

            public DateTime StartTime
            {
                get => _startTime;
                set => _startTime = value.ToUniversalTime();
            }

            public DateTime EndTime
            {
                get => _endTime;
                set => _endTime = value.ToUniversalTime();
            }

            public decimal Level
            {
                get => _level;
                set => _level = 1000000000000m * value;
            }
        }
    }
}