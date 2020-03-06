using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bot.NamebaseClient.Responses;
using Microsoft.Extensions.Logging;

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
                    decimal.Parse(x[1]) > avgDepth + std / 1.5m && decimal.Parse(x[1]) != Bottom) ?? sorted.Last();

            var ordered = data.Select(x => (Level: decimal.Parse(x[0]), Amount: decimal.Parse(x[1])))
                .OrderBy(x => x.Level * (higherIsBetter ? -1 : 1)).ToImmutableList();

            var peaks = new List<(decimal Level, decimal TotalAmount)>();
            var totalAmount = ordered[0].Amount;

            for(var i = 1; i < ordered.Count; i++)
            {
                totalAmount += ordered[i].Amount;
                // consider it a peak if it's larger than 1 std deviation
                if (ordered[i].Amount > avgDepth + std / 1m)
                {
                    peaks.Add((ordered[i].Level, TotalAmount: totalAmount));
                }
            }

            Resistance = peaks;
        }

        public decimal Bottom { get; set; }
        public List<(decimal Level, decimal TotalAmount)> Resistance { get; set; } 

        public DateTime Timestamp { get; set; }
    }
}