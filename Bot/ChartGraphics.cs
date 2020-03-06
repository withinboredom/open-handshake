using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Bot.NamebaseClient.Responses;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Core.Drawing;
using OxyPlot.Series;

namespace Bot
{
    public static class ChartGraphics
    {
        public struct Row
        {
            private decimal _bottomCeiling;
            private decimal _topCeiling;
            public DateTime Time { get; set; }
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

        public struct OrderPosition
        {
            private decimal _level;
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }

            public decimal Level
            {
                get => _level;
                set => _level = 1000000000000m * value;
            }
        }

        public struct OrderFill
        {
            private decimal _level;
            public DateTime Time { get; set; }

            public decimal Level
            {
                get => _level;
                set => _level = value * 1000000000000m;
            }

            public bool IsFilled { get; set; }
            public OrderSide Side { get; set; }
        }
        
        public static Dictionary<string, List<Row>> Data { get; set; } = new Dictionary<string, List<Row>>();
        public static Dictionary<long, OrderPosition> OrderPositions { get; set; } = new Dictionary<long, OrderPosition>();
        public static List<OrderFill> OrderFills { get; set; } = new List<OrderFill>();

        public static void WriteChart(string name, string title)
        {
            var data = Data[name].OrderBy(x => x.Time).ToList();

            if (data.Count == 0) return;
            
            var pm = new PlotModel
            {
                Title = title,
            };
            
            pm.Axes.Add(new DateTimeAxis()
            {
                Position = AxisPosition.Bottom,
                IntervalType = DateTimeIntervalType.Auto,
                StringFormat = "HH:mm",
                Minimum = DateTimeAxis.ToDouble(data.Min(x => x.Time)),
                Maximum = DateTimeAxis.ToDouble(data.Max(x => x.Time))
            });

            var min = (double)
                data.Min(x => Math.Min(x.High, Math.Min(x.Low, Math.Min(x.Raw, Math.Min(x.MidPoint, Math.Min(x.BottomCeiling, x.TopCeiling))))));
            var max = (double)
                data.Max(x => Math.Max(x.High, Math.Max(x.Low, Math.Max(x.Raw, Math.Min(x.MidPoint, Math.Min(x.BottomCeiling, x.TopCeiling))))));
                
            var mid = (max - min) / 2 + min;
            pm.Axes.Add(new LinearAxis()
            {
                Position = AxisPosition.Left,
                Minimum = min,
                Maximum = max,
            });

            var midPointSeries = new LineSeries() {Color = OxyColors.Red};
            var upSeries = new LineSeries() { Color = OxyColors.Cyan };
            var dnSeries = new LineSeries() { Color = OxyColors.Cyan };
            var rlSeries = new LineSeries() {Color = OxyColors.Black};
            var signalSeries = new LineSeries() {Color = OxyColors.Green};
            var orderSells = new ScatterSeries() { MarkerFill = OxyColors.Red};
            var orderBuys = new ScatterSeries() { MarkerFill = OxyColors.Green};
            var paritals = new ScatterSeries() {MarkerFill = OxyColors.Yellow};
            var greenTransparent = OxyColor.FromArgb(50, 0, 255, 0);
            var redTransparent = OxyColor.FromArgb(50, 255, 0, 0);
            var bottom = new AreaSeries()
            {
                LineStyle = LineStyle.Solid,
                Color = OxyColors.Red, 
                Color2 = OxyColors.Transparent,
                Fill = redTransparent,
                DataFieldX2 = "X",
                ConstantY2 = min,
            };
            var top = new AreaSeries()
            {
                Color = OxyColors.Green, 
                Color2 = OxyColors.Transparent,
                LineStyle = LineStyle.Solid,
                Fill = greenTransparent,
                DataFieldX2 = "X",
                ConstantY2 = max,
            };

            var x = 0d;
            foreach (var item in data)
            {
                x = DateTimeAxis.ToDouble(item.Time);

                midPointSeries.Points.Add(new DataPoint(x, (double)item.MidPoint));
                upSeries.Points.Add(new DataPoint(x, (double)item.High));
                dnSeries.Points.Add(new DataPoint(x, (double)item.Low));
                rlSeries.Points.Add(new DataPoint(x, (double)item.Raw));
                signalSeries.Points.Add(new DataPoint(x, item.Signal == 0 ? mid : (item.Signal < 0 ? min : max)));
                bottom.Points.Add(new DataPoint(x, (double)item.BottomCeiling));
                top.Points.Add(new DataPoint(x, (double)item.TopCeiling));
                bottom.Points2.Add(new DataPoint(x, min));
                top.Points2.Add(new DataPoint(x, max));
            }

            foreach (var position in OrderPositions)
            {
                var series = new LineSeries() {Color = OxyColors.Fuchsia};
                var startX = DateTimeAxis.ToDouble(position.Value.StartTime);
                var endX = DateTimeAxis.ToDouble(position.Value.EndTime);
                series.Points.Add(new DataPoint(startX, (double)position.Value.Level));
                series.Points.Add(new DataPoint(endX, (double)position.Value.Level));
                pm.Series.Add(series);
            }

            foreach (var order in OrderFills)
            {
                x = DateTimeAxis.ToDouble(order.Time);
                var point = new ScatterPoint(x, (double) order.Level);
                if (order.IsFilled)
                {
                    if (order.Side == OrderSide.BUY)
                    {
                        orderBuys.Points.Add(point);
                    }
                    else
                    {
                        orderSells.Points.Add(point);
                    }
                }
                else
                {
                    paritals.Points.Add(point);
                }
            }

            pm.Series.Add(midPointSeries);
            pm.Series.Add(upSeries);
            pm.Series.Add(dnSeries);
            pm.Series.Add(rlSeries);
            pm.Series.Add(signalSeries);
            pm.Series.Add(orderBuys);
            pm.Series.Add(orderSells);
            pm.Series.Add(paritals);
            pm.Series.Add(bottom);
            pm.Series.Add(top);

            PngExporter.Export(pm, name + ".png", 3840, 2160, OxyColor.FromRgb(255,255,255));
        }
    }
}