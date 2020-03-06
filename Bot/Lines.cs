using System;
using System.Collections.Generic;
using System.Linq;
using Bot.NamebaseClient;
using Bot.NamebaseClient.Responses;

namespace Bot
{
    public class Lines
    {
        private bool _wroteCenter;

        public void OrderCenterLine(CeilingData Top, CeilingData Bottom)
        {
            if (_wroteCenter) return;
            Console.ResetColor();
            ConsoleBox.WriteLine(
                $"Center: {Top.Bottom} -- {Bottom.Bottom} ({Bottom.Bottom - Top.Bottom})");
            _wroteCenter = true;
        }

        internal static decimal PercentChanged(decimal v1, decimal v2)
        {
            if (v1 == 0) return 0;
            return (v2 - v1) / v1 * 100m;
        }

        public string CurrentBalanceLine(TrackableValue balance, string currency, decimal? convertedValue = null,
            decimal? totalValue = null)
        {
            var str =
                $"Current balance: {balance.LatestValue:N6}{currency} ({balance.Difference:+#,###.####;-#,###.####;0.0000} | {PercentChanged(balance.InitialValue, balance.LatestValue):N2}%) ";
            if (convertedValue != null) str += $" value {convertedValue ?? 0:N4} == {totalValue ?? 0:N4}";

            return str;
        }

        public string CenterLine(CeilingData? center, CeilingData? real, OrderSide side, decimal directionIndicator)
        {
            var strSide = side == OrderSide.BUY ? "buy " : "sell";
            return center == null || real == null
                ? "Last {side} center: N/A"
                : $"Last {strSide} center: {center.Bottom:N8} -> {center.Resistance[0].Level:N8} %ΔB {PercentChanged(center?.Bottom ?? 0m, real.Bottom):N2} %ΔC {PercentChanged(center?.Resistance[0].Level ?? 0m, real.Resistance[0].Level):N2} [{directionIndicator:N5}]";
        }

        public void Box(params string[] lines)
        {
            var max = lines.Max(x => x.Length);
            var border = new string('-', max + 2);
            ConsoleBox.WriteLine(border);
            foreach (var line in lines) ConsoleBox.WriteLine(line.PadRight(max) + " |");

            ConsoleBox.WriteLine(border);
        }

        public decimal OrderChart(IEnumerable<ObservableOrder> orders, CeilingData? selling, CeilingData? buying,
            decimal sellPrediction, decimal buyPrediction)
        {
            if (selling == null || buying == null) return 0;
            OrderSide? lastSide = null;
            var counter = 0;
            var potentialBalance = 0m;
            var renderedLines = 0;
            foreach (var order in orders.OrderBy(x => -x.Price))
            {
                if (order.IsDeleted)
                {
                    continue;
                }

                renderedLines += 1;
                
                if (lastSide != null && lastSide != order.RawOrder.Side)
                {
                    OrderCenterLine(selling, buying);
                    counter = 0;
                }

                lastSide = order.RawOrder.Side;

                var color = order.RawOrder switch
                {
                    { } x when x.Status == OrderStatus.PARTIALLY_FILLED => ConsoleColor.Yellow,
                    { } x when x.Side == OrderSide.SELL &&
                               x.Status == OrderStatus.FILLED => ConsoleColor.DarkRed,
                    { } x when x.Side == OrderSide.SELL && buyPrediction > x.Price => ConsoleColor.Cyan,
                    { } x when x.Side == OrderSide.BUY && sellPrediction < x.Price => ConsoleColor.Cyan,
                    { } x when x.Side == OrderSide.SELL &&
                               x.Status != OrderStatus.FILLED => ConsoleColor.Red,
                    { } x when x.Side == OrderSide.BUY &&
                               x.Status != OrderStatus.FILLED => ConsoleColor.Green,
                    { } x when x.Side == OrderSide.BUY &&
                               x.Status == OrderStatus.FILLED => ConsoleColor.DarkGreen,
                    { } x when x.Status == OrderStatus.CANCELED || x.Status == OrderStatus.CLOSED => ConsoleColor.Black,
                    _ => ConsoleColor.Gray
                };

                var postfix = string.Empty;

                if (order.RawOrder.Side == OrderSide.SELL && buyPrediction > order.Price ||
                    order.RawOrder.Side == OrderSide.BUY && sellPrediction < order.Price)
                    postfix = " X";

                Console.ForegroundColor = color;

                ConsoleBox.WriteLine(
                    $"{counter++}. {order.RawOrder.Side.ToString()} order for {order.RawOrder.ExecutedQuantity:N4}/{order.RawOrder.OriginalQuantity:N4} @ {order.Price}{postfix}");
                potentialBalance += order.RawOrder.Side == OrderSide.BUY
                    ? order.RawOrder.OriginalQuantity - order.RawOrder.ExecutedQuantity
                    : 0m;
            }

            OrderCenterLine(selling, buying);

            for (var i = renderedLines; i < orders.Count(); i++)
            {
                ConsoleBox.WriteLine("");
            }

            Console.ResetColor();

            return potentialBalance;
        }
    }
}