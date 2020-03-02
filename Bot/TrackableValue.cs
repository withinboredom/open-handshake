using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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

        public (decimal, Func<DateTime, decimal>) Predict()
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

                return (slope, x => slope * (decimal) (x - start.Value).TotalSeconds + yintercept);
            }
            catch
            {
                return (0m, x => 0);
            }
        }

        public IEnumerable<TracedValue> Values => _values.ToImmutableList();
    }
}