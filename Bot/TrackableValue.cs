using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Bot.NamebaseClient;

namespace Bot
{
    public class TrackableValue
    {
        private decimal? _initialValue;
        private decimal _lastValue;

        public decimal LatestValue
        {
            get => _lastValue;
            set
            {
                if (_initialValue == null) _initialValue = value;

                _lastValue = value;
            }
        }

        public decimal InitialValue => _initialValue ?? 0m;
        public decimal Difference => _lastValue - _initialValue ?? _lastValue;
    }

    public struct TracableValue
    {
        public struct TracedValue
        {
            public decimal Value { get; set; }
            public DateTime AddedAt { get; set; }
        }

        private readonly Queue<TracedValue> _values;
        private decimal? _lastValue;
        private readonly int _maxSamples;

        public TracableValue(int maxSamples)
        {
            _values = new Queue<TracedValue>();
            _maxSamples = maxSamples;
            _lastValue = null;
        }

        public decimal? LatestValue
        {
            get => _lastValue;
            set
            {
                if (!value.HasValue) return;

                _lastValue = value;
                if (_values.Count >= _maxSamples) _values.Dequeue();
                _values.Enqueue(new TracedValue
                {
                    AddedAt = DateTime.Now,
                    Value = value.Value
                });
            }
        }

        public static decimal StdDeviation(IEnumerable<decimal> data, decimal mean)
        {
            var sumOfSquares = data.Select(x => (x - mean) * (x - mean)).Sum();
            return (decimal) Math.Sqrt((double) sumOfSquares / (data.Count() - 1));
        }

        public (int Signal, decimal Slope) Signal(int lag, decimal threshold, decimal influence, ObservableCenterPoint center)
        {
            var input = _values.ToList();

            if (input.Count < lag) return (0, 0);

            var signals = new int[input.Count];
            var filteredY = input.Select(x => x.Value).ToArray();
            var avgFilter = new decimal[input.Count];
            var stdFilter = new decimal[input.Count];
            
            var sloper = new TracableValue(input.Count);

            var initialWindow = new List<decimal>(filteredY).Skip(0).Take(lag).ToList();
            avgFilter[lag - 1] = initialWindow.Average();
            stdFilter[lag - 1] = StdDeviation(initialWindow, initialWindow.Average());

            for (var i = lag; i < input.Count; i++)
            {
                if (Math.Abs(input[i].Value - avgFilter[i - 1]) > threshold * stdFilter[i - 1])
                {
                    signals[i] = (input[i].Value > avgFilter[i - 1]) ? 1 : -1;
                    filteredY[i] = influence * input[i].Value + (1 - influence) * filteredY[i - 1];
                }
                else
                {
                    signals[i] = 0;
                    filteredY[i] = input[i].Value;
                }

                var slidingWindow = new List<decimal>(filteredY).Skip(i - lag).Take(lag + 1).ToList();

                var tmpMean = slidingWindow.Average();
                var tmpStdDev = StdDeviation(slidingWindow, tmpMean);

                avgFilter[i] = tmpMean;
                stdFilter[i] = tmpStdDev;
                sloper.LatestValue = tmpMean;
            }
            
            File.AppendAllLines("robot.csv", new []{$"{DateTime.Now},{avgFilter.Last()},{avgFilter.Last() + threshold * stdFilter.Last()},{avgFilter.Last() - threshold * stdFilter.Last()},{signals.Last()},{sloper.Predict().Slope}"});
            ChartGraphics.Data["robot"].Add(new ChartGraphics.Row
            {
                High = avgFilter.Last() + threshold * stdFilter.Last(),
                Low = avgFilter.Last() - threshold * stdFilter.Last(),
                MidPoint = avgFilter.Last(),
                Raw = input.Last().Value,
                Signal = signals.Last(),
                Time = input.Last().AddedAt,
                BottomCeiling = center.BuySide.Bottom,
                TopCeiling = center.SellSide.Bottom,
            });

            return (signals.Last(), sloper.Predict().Slope);
        }

        public (decimal Slope, Func<DateTime, decimal> PredictX, Func<decimal, DateTime> PredictY) Predict()
        {
            var sumOf = (0m, 0m);
            var sumOfSqr = (0m, 0m);
            var ss = (0m, 0m);
            var sumCodviates = 0m;
            var sCo = 0m;
            var count = 0m;
            DateTime? start = null;

            foreach (var datum in Values.OrderBy(x => x.AddedAt))
            {
                if (!start.HasValue) start = datum.AddedAt;

                count += 1;
                var x = (decimal) (datum.AddedAt - start.Value).TotalSeconds;
                var y = datum.Value;
                sumCodviates += x * y;
                sumOf.Item1 += x;
                sumOf.Item2 += y;
                sumOfSqr.Item1 += x * y;
                sumOfSqr.Item2 += y * y;
            }

            try
            {
                ss.Item1 = sumOfSqr.Item1 - sumOf.Item1 * sumOf.Item1 / count;
                ss.Item2 = sumOfSqr.Item2 - sumOf.Item2 * sumOf.Item2 / count;
                //var numerator = (count * sumCodviates) - (sumOf.Item1 * sumOf.Item2);
                //var denominator = (count * sumOfSqr.Item1 - (sumOf.Item1 * sumOf.Item1)) *
                //                  (count * sumOfSqr.Item2 - (sumOf.Item2 * sumOf.Item2));
                sCo = sumCodviates - sumOf.Item1 * sumOf.Item2 / count;

                var mean = (sumOf.Item1 / count, sumOf.Item2 / count);
                //var db1r = numerator / (decimal) Math.Sqrt((double)denominator);
                //var rsqr = db1r * db1r;
                var yintercept = mean.Item2 - sCo / ss.Item1 * mean.Item1;
                var slope = sCo / ss.Item1;

                return (Slope: slope, PredictX: x => slope * (decimal) (x - start.Value).TotalSeconds + yintercept, PredictY: y =>
                {
                    if(slope == 0)
                    {
                        return DateTime.MaxValue;
                    }

                    try
                    {
                        return start.Value + TimeSpan.FromSeconds((double) ((y - yintercept) / slope));
                    } catch
                    {
                        return DateTime.MaxValue;
                    }
                });
            }
            catch
            {
                return (0m, x => 0, x => DateTime.MaxValue);
            }
        }

        public IEnumerable<TracedValue> Values => _values.ToImmutableList();
    }
}