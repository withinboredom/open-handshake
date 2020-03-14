using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using Bot.NamebaseClient.Responses;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Core.Drawing;
using OxyPlot.Series;

namespace Bot.Charting
{
    public static partial class ChartGraphics
    {
        public static Dictionary<string, List<Row>> Data { get; set; } = new Dictionary<string, List<Row>>();
        public static Dictionary<long, OrderPosition> OrderPositions { get; set; } = new Dictionary<long, OrderPosition>();
        public static List<OrderFill> OrderFills { get; set; } = new List<OrderFill>();
        public static GraphableVector Asks { get; set; } = new GraphableVector();
        public static GraphableVector Bids { get; set; } = new GraphableVector();

        public static List<Point> HnsBalance { get; set; } = new List<Point>();
        public static List<Point> PerfectBalance { get; set; } = new List<Point>();

        private static double ToSats(double raw)
        {
            return raw / 10000d;
        }

        private static double ToSats(decimal raw)
        {
            return (double) (raw / 10000m);
        }

        private static List<T> CreateKeyList<T, D>(Dictionary<T, D> data)
        {
            return data.Keys.ToList();
        }

        private static void PruneOldData()
        {
            try
            {
                foreach (var key in CreateKeyList(Data))
                {
                    Data[key].RemoveAll(x => x.Time < DateTime.UtcNow - TimeSpan.FromHours(2));
                }

                foreach (var key in CreateKeyList(OrderPositions))
                {
                    if (OrderPositions[key].EndTime < DateTime.UtcNow - TimeSpan.FromHours(2))
                    {
                        try
                        {
                            OrderPositions.Remove(key);
                        }
                        catch
                        {
                            // nothing
                        }
                    }
                }

                OrderFills.RemoveAll(x => x.Time < DateTime.UtcNow - TimeSpan.FromHours(2));
                HnsBalance.RemoveAll(x => x.Time < DateTime.UtcNow - TimeSpan.FromHours(2));
                Asks.Data.RemoveAll(x => x.x < DateTime.UtcNow - TimeSpan.FromHours(2));
                Bids.Data.RemoveAll(x => x.x < DateTime.UtcNow - TimeSpan.FromHours(2));
            }
            catch
            {
                // dammit
            }
        }

        private static int FontSize = 42;
        
        public static void WriteBalanceChart()
        {
            var pm = new PlotModel
            {
                Title = "Trading Balance",
                TitleFontSize = FontSize,
            };

            pm.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                IntervalType = DateTimeIntervalType.Auto,
                StringFormat = "HH:mm",
                Maximum = DateTimeAxis.ToDouble(HnsBalance.Max(x => x.Time)),
                Minimum = DateTimeAxis.ToDouble(HnsBalance.Min(x => x.Time)),
                IntervalLength = 120,
                FontSize = FontSize,
            });

            var minY = (double) Math.Min(HnsBalance.Min(x => x.Value), PerfectBalance.Min(x => x.Value)) - 10d;
            var maxY = (double) Math.Max(HnsBalance.Max(x => x.Value), PerfectBalance.Max(x => x.Value)) + 10d;
            
            pm.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Maximum = maxY,
                Minimum = minY,
                FontSize = FontSize,
            });
            
            var series = new LineSeries
            {
                Color = OxyColors.Green,
                Title = "Market Sell"
            };

            foreach (var item in HnsBalance)
            {
                series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(item.Time), (double)item.Value));
            }
            
            pm.Series.Add(series);

            pm.IsLegendVisible = true;
            pm.LegendPosition = LegendPosition.LeftTop;
            pm.LegendFontSize = FontSize;
            
            series = new LineSeries
            {
                Color = OxyColors.Black,
                Title = "Orders filled and market sell remainder",
            };

            foreach (var item in PerfectBalance)
            {
                series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(item.Time), (double)item.Value));
            }
            
            pm.Series.Add(series);
            
            WriteFile("balance.svg", SvgExporter.ExportToString(pm, 3840, 2160, false));
        }

        private enum AxisKeys
        {
            Level,
            Time,
            Asks,
            Bids,
        }

        private static void WriteFile(string file, string contents)
        {
            File.WriteAllText(file + ".tmp", contents);
            File.Delete(file);
            File.Move(file + ".tmp", file);
        }
        
        public static void WriteChart(string name, string title)
        {
            PruneOldData();
            WriteBalanceChart();
            
            var data = Data[name].OrderBy(x => x.Time).ToList();

            if (data.Count == 0) return;
            
            var pm = new PlotModel
            {
                Title = title,
                TitleFontSize = FontSize,
            };
            var askData = Asks.Raster();
            var bidData = Bids.Raster();
            
            pm.Axes.Add(new DateTimeAxis()
            {
                Position = AxisPosition.Bottom,
                IntervalType = DateTimeIntervalType.Auto,
                StringFormat = "HH:mm",
                Minimum = DateTimeAxis.ToDouble(data.Min(x => x.Time)),
                Maximum = DateTimeAxis.ToDouble(data.Max(x => x.Time)),
                IntervalLength = 120,
                Key = AxisKeys.Time.ToString(),
                FontSize = FontSize,
            });
            
            pm.Axes.Add(new LinearColorAxis
            {
                Position = AxisPosition.Right,
                Palette = OxyPalette.Interpolate(20, OxyColors.Red, OxyColors.OrangeRed, OxyColors.Orange, OxyColors.DarkRed),
                Key = AxisKeys.Asks.ToString(),
                FontSize = FontSize - FontSize / 4,
                LowColor = OxyColor.FromArgb(1,0,0,0),
                Minimum = 1,
                Maximum = askData.Average,
                HighColor = OxyColors.DarkRed,
                Title = "Sell Volume",
            });
            
            pm.Axes.Add(new LinearColorAxis
            {
                Position = AxisPosition.Top,
                Palette = OxyPalette.Interpolate(20, OxyColors.LimeGreen, OxyColors.LawnGreen, OxyColors.ForestGreen, OxyColors.DarkGreen),
                Key = AxisKeys.Bids.ToString(),
                FontSize = FontSize - FontSize / 4,
                LowColor = OxyColor.FromArgb(1,0,0,0),
                Minimum = 1,
                Maximum = bidData.Average,
                HighColor = OxyColors.DarkGreen,
                Title = "Buy Volume",
            });

            var min =
                ToSats(data.Min(x => Math.Min(x.High, Math.Min(x.Low, Math.Min(x.Raw, Math.Min(x.MidPoint, Math.Min(x.BottomCeiling, x.TopCeiling))))))) - 10;
            var max =
                ToSats(data.Max(x => Math.Max(x.High, Math.Max(x.Low, Math.Max(x.Raw, Math.Min(x.MidPoint, Math.Min(x.BottomCeiling, x.TopCeiling))))))) + 10;
                
            var mid = (max - min) / 2 + min;
            pm.Axes.Add(new LinearAxis()
            {
                Position = AxisPosition.Left,
                Minimum = min - 100,
                Maximum = max + 100,
                Key = AxisKeys.Level.ToString(),
                FontSize = FontSize,
            });

            var midPointSeries = new LineSeries() {Color = OxyColors.Red, MarkerSize = 5};
            var upSeries = new LineSeries() { Color = OxyColors.Cyan, MarkerSize = 5};
            var dnSeries = new LineSeries() { Color = OxyColors.Cyan, MarkerSize = 5};
            var rlSeries = new LineSeries() {Color = OxyColors.Black, MarkerSize = 5};
            var signalSeries = new LineSeries() {Color = OxyColors.Green};
            var orderSells = new ScatterSeries() { MarkerFill = OxyColors.Red, MarkerSize = 5};
            var orderBuys = new ScatterSeries() { MarkerFill = OxyColors.Green, MarkerSize = 5};
            var paritals = new ScatterSeries() {MarkerFill = OxyColors.Yellow, MarkerSize = 5};
            var greenTransparent = OxyColor.FromArgb(30, 0, 255, 0);
            var redTransparent = OxyColor.FromArgb(30, 255, 0, 0);
            var bottom = new AreaSeries()
            {
                LineStyle = LineStyle.Solid,
                Color = OxyColors.Red, 
                Color2 = OxyColors.Transparent,
                Fill = greenTransparent,
                DataFieldX2 = "X",
                ConstantY2 = min - 100,
            };
            var top = new AreaSeries()
            {
                Color = OxyColors.Green, 
                Color2 = OxyColors.Transparent,
                LineStyle = LineStyle.Solid,
                Fill = redTransparent,
                DataFieldX2 = "X",
                ConstantY2 = max + 100,
            };

            var x = 0d;
            foreach (var item in data)
            {
                x = DateTimeAxis.ToDouble(item.Time);

                midPointSeries.Points.Add(new DataPoint(x, ToSats(item.MidPoint)));
                upSeries.Points.Add(new DataPoint(x, ToSats(item.High)));
                dnSeries.Points.Add(new DataPoint(x, ToSats(item.Low)));
                rlSeries.Points.Add(new DataPoint(x, ToSats(item.Raw)));
                signalSeries.Points.Add(new DataPoint(x, item.Signal == 0 ? mid : (item.Signal < 0 ? min : max)));
                bottom.Points.Add(new DataPoint(x, ToSats(item.BottomCeiling)));
                top.Points.Add(new DataPoint(x, ToSats(item.TopCeiling)));
                bottom.Points2.Add(new DataPoint(x, min - 100));
                top.Points2.Add(new DataPoint(x, max + 100));
            }

            foreach (var position in OrderPositions)
            {
                var series = new LineSeries() {Color = OxyColors.Fuchsia, MarkerSize = 5};
                var startX = DateTimeAxis.ToDouble(position.Value.StartTime);
                var endX = DateTimeAxis.ToDouble(position.Value.EndTime);
                series.Points.Add(new DataPoint(startX, ToSats(position.Value.Level)));
                series.Points.Add(new DataPoint(endX, ToSats(position.Value.Level)));
                pm.Series.Add(series);
            }

            foreach (var order in OrderFills)
            {
                x = DateTimeAxis.ToDouble(order.Time);
                var point = new ScatterPoint(x, ToSats(order.Level));
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
            
            var asks = new HeatMapSeries
            {
                X0 = askData.Min.X,
                X1 = askData.Max.X,
                Y0 = askData.Min.Y,
                Y1 = askData.Max.Y,
                XAxisKey = AxisKeys.Time.ToString(),
                ColorAxisKey = AxisKeys.Asks.ToString(),
                YAxisKey = AxisKeys.Level.ToString(),
                Interpolate = true,
                RenderMethod = HeatMapRenderMethod.Bitmap,
                Data = askData.Rendered,
                CoordinateDefinition = HeatMapCoordinateDefinition.Edge,
            };
            
            var bids = new HeatMapSeries
            {
                X0 = bidData.Min.X,
                X1 = bidData.Max.X,
                Y0 = bidData.Min.Y,
                Y1 = bidData.Max.Y,
                XAxisKey = AxisKeys.Time.ToString(),
                ColorAxisKey = AxisKeys.Bids.ToString(),
                YAxisKey = AxisKeys.Level.ToString(),
                Interpolate = true,
                RenderMethod = HeatMapRenderMethod.Bitmap,
                Data = bidData.Rendered,
                CoordinateDefinition = HeatMapCoordinateDefinition.Edge,
            };
            
            pm.Series.Add(asks);
            pm.Series.Add(bids);
            pm.Series.Add(midPointSeries);
            pm.Series.Add(upSeries);
            pm.Series.Add(dnSeries);
            pm.Series.Add(rlSeries);
            //pm.Series.Add(signalSeries);
            pm.Series.Add(orderBuys);
            pm.Series.Add(orderSells);
            pm.Series.Add(paritals);
            pm.Series.Add(bottom);
            pm.Series.Add(top);
            
            WriteFile(name + ".svg", SvgExporter.ExportToString(pm, 3840, 2160, false));
        }
    }
}