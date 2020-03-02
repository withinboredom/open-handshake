using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.NamebaseClient
{
    public class CeilingData
    {
        public CeilingData(List<List<string>> data, bool higherIsBetter)
        {
            Timestamp = DateTime.UtcNow;
            Bottom = decimal.Parse(higherIsBetter ? data.Max(x => x[0]) : data.Min(x => x[0]));

            var avgDepth = data.Average(x => decimal.Parse(x[1]));
            var sumOfSquares = data.Select(x =>
            {
                var dec = decimal.Parse(x[1]);
                return (dec - avgDepth) * (dec - avgDepth);
            }).Sum();
            var std = (decimal) Math.Sqrt((double) sumOfSquares / data.Count);

            var sorted = data.OrderBy(x => decimal.Parse(x[0]) * (higherIsBetter ? -1m : 1m));

            // find first that is greater than 2 std deviations
            var ceilingList =
                sorted.FirstOrDefault(x =>
                    decimal.Parse(x[1]) > avgDepth + std / 1m && decimal.Parse(x[1]) != Bottom) ?? sorted.Last();

            Ceiling = decimal.Parse(ceilingList[0]);
        }

        public decimal Bottom { get; set; }
        public decimal Ceiling { get; set; }

        public DateTime Timestamp { get; set; }
    }
}