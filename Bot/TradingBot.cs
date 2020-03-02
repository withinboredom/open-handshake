using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.NamebaseClient;
using Bot.NamebaseClient.Requests;
using Bot.NamebaseClient.Responses;
using Microsoft.Extensions.Logging;

namespace Bot
{
    public class TradingBot
    {
        private readonly ObservableAccount _account;
        private readonly List<ObservableOrder> _buys;
        private readonly ObservableCenterPoint _center;
        private readonly Client _client;
        public Configuration _config { get; set; }
        private readonly ILogger _logger;
        private readonly List<ObservableOrder> _sells;
        private readonly ConsoleBox _logs = new ConsoleBox("Logs");
        private Client.Heavy _buyHeavy;
        private CeilingData? _buyPoint;
        private DateTime _buyTime;
        private DateTime _now;
        private Client.Heavy _sellHeavy;
        private CeilingData? _sellPoint;
        private DateTime _sellTime;
        private Command _buyCommand, _sellCommand;

        private enum Command
        {
            None,
            PriorityUpdate,
            DelayedUpdate
        }

        private TradingBot(Client client, ObservableAccount account, ObservableCenterPoint center, Configuration config, ILogger logger)
        {
            _client = client;
            _account = account;
            _center = center;
            _config = config;
            _logger = logger;
            _account.BtcUpdated += AccountOnBtcUpdated;
            _account.HnsUpdated += AccountOnHnsUpdated;
            _center.BuyCeilingChanged += CenterOnBuyCeilingChanged;
            _center.SellCeilingChanged += CenterOnSellCeilingChanged;
            _buys = new List<ObservableOrder>(_config.NumberOrders);
            _sells = new List<ObservableOrder>(_config.NumberOrders);
            _sellHeavy = Client.Heavy.None;
            _buyHeavy = Client.Heavy.None;
            _buyTime = DateTime.Now;
            _sellTime = DateTime.Now;
            _now = DateTime.Now;
            _logs.MaxLines = 10;

            ListLogger.Stream.CollectionChanged += StreamOnCollectionChanged;
        }

        private void StreamOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch(e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach(var line in e.NewItems)
                    {
                        if (line is ListLogger.LogLine log)
                        {
                            _logs.Lines.Add(new ConsoleBox.Line
                            {
                                Content = log.Content,
                                Color = log.Level switch
                                {
                                    LogLevel.None => ConsoleColor.White,
                                    LogLevel.Error => ConsoleColor.Red,
                                    LogLevel.Critical => ConsoleColor.DarkRed,
                                    LogLevel.Debug => ConsoleColor.White,
                                    LogLevel.Information => ConsoleColor.Cyan,
                                    LogLevel.Trace => ConsoleColor.White,
                                    LogLevel.Warning => ConsoleColor.Yellow,
                                    _ => ConsoleColor.White
                                }
                            });
                        }
                    }
                    ListLogger.Stream.Clear();

                    break;
            }
        }

        private void CenterOnSellCeilingChanged(object sender, ObservableCenterPoint.CeilingChangedEventArgs e)
        {
            _logger.LogInformation("Detected sell side ceiling change");
            var prediction = _center.PredictSale(DateTime.Now.AddSeconds(5));

            var currentValueHns = (_account.Hns.Total - _config.HnsZero) /
                                  Client.ConvertBtcToHns(_account.Btc.Total - _config.BtcZero, _center.SellSide.Bottom);

            if (_config.HnsRatio > 0)
            {
                if (currentValueHns < _config.HnsRatio)
                {
                    if (_sellHeavy != Client.Heavy.Bottom)
                    {
                        (_sellTime, _sellCommand) = SetTime(_sellTime, _now.AddMinutes(5));
                    }

                    _sellHeavy = Client.Heavy.Bottom;
                }
                else if (currentValueHns > _config.HnsRatio)
                {
                    if (_sellHeavy != Client.Heavy.Top)
                    {
                        (_sellTime, _sellCommand) = SetTime(_sellTime, _now.AddMinutes(5));
                    }

                    _sellHeavy = Client.Heavy.Top;
                }
                else
                {
                    if (_sellHeavy != Client.Heavy.None)
                    {
                        (_sellTime, _sellCommand) = SetTime(_sellTime, _now.AddMinutes(5));
                    }

                    _sellHeavy = Client.Heavy.None;
                }
            }

            _logger.LogInformation($"Setting sell priority {_sellHeavy}");

            var percentBottom = Math.Abs(Lines.PercentChanged(_sellPoint?.Bottom ?? 0, e.New.Bottom));
            var percentTop = Math.Abs(Lines.PercentChanged(_sellPoint?.Ceiling ?? 0, e.New.Ceiling));

            if (percentBottom <= _config.SellBottomChange && percentTop <= _config.SellTopChange) return;

            _logger.LogInformation($"Major change detected, recalculating sell side order book");

            (_sellTime, _sellCommand) = SetTime(_sellTime, _now);
        }

        private (DateTime, Command) SetTime(DateTime other, DateTime? set = null)
        {
            if (set == null) set = _now;

            if (set <= _now)
            {
                return (set.Value, Command.PriorityUpdate);
            }

            return (other > set.Value ? other : set.Value, Command.DelayedUpdate);
        }

        private void CenterOnBuyCeilingChanged(object sender, ObservableCenterPoint.CeilingChangedEventArgs e)
        {
            _logger.LogInformation("Detected buy side ceiling change");
            var prediction = _center.PredictBuy(DateTime.Now.AddSeconds(5));
            
            // todo: use prediction
            var curentValueBtc = Client.ConvertBtcToHns(_account.Btc.Total - _config.BtcZero, _center.SellSide.Bottom) /
                                 (_account.Hns.Total - _config.HnsZero);

            if (_config.BtcRatio > 0)
            {
                if (curentValueBtc < _config.BtcRatio)
                {
                    if (_buyHeavy != Client.Heavy.Bottom)
                    {
                        (_buyTime, _buyCommand) = SetTime(_buyTime, _now.AddMinutes(5));
                    }

                    _buyHeavy = Client.Heavy.Bottom;
                }
                else if (curentValueBtc > _config.BtcRatio)
                {
                    if (_buyHeavy != Client.Heavy.Top)
                    {
                        (_buyTime, _buyCommand) = SetTime(_buyTime, _now.AddMinutes(5));
                    }

                    _buyHeavy = Client.Heavy.Top;
                }
                else
                {
                    if (_buyHeavy != Client.Heavy.None)
                    {
                        (_buyTime, _buyCommand) = SetTime(_buyTime, _now.AddMinutes(5));
                    }

                    _buyHeavy = Client.Heavy.None;
                }
            }

            _logger.LogInformation($"Setting buy priority {_buyHeavy}");

            var percentBottom = Math.Abs(Lines.PercentChanged(_buyPoint?.Bottom ?? 0, e.New.Bottom));
            var percentTop = Math.Abs(Lines.PercentChanged(_buyPoint?.Ceiling ?? 0, e.New.Ceiling));

            if (percentBottom <= _config.SellBottomChange && percentTop <= _config.SellTopChange) return;

            (_buyTime, _buyCommand) = SetTime(_buyTime, _now);
            _logger.LogInformation($"Major change detected, recalculating buy side order book");
        }

        public static async Task<TradingBot> CreateInstance(Client client, Configuration config, ILogger logger)
        {
            Console.Write("Initializing bot, please wait...");
            var account = await client.GetAccount();
            var center = await client.GetCenterPoint();
            var bot = new TradingBot(client, new ObservableAccount(client, account),
                new ObservableCenterPoint(client, center), config, logger);
            Console.WriteLine("Bot initialized!");
            return bot;
        }

        private void AccountOnBtcUpdated(object sender, ObservableAccount.BalanceUpdatedEventArgs e)
        {
            if(Lines.PercentChanged(e.PreviousAmount, e.NewAmount) > 0.15m)
            {
                (_buyTime, _buyCommand) = SetTime(_buyTime, _now.AddSeconds(30));
            }
            _logger.LogInformation($"Detected change in BTC: {e.NewAmount - e.PreviousAmount:N8}");
        }

        private void AccountOnHnsUpdated(object sender, ObservableAccount.BalanceUpdatedEventArgs e)
        {
            if (Lines.PercentChanged(e.PreviousAmount, e.NewAmount) > 0.15m)
            {
                (_sellTime, _sellCommand) = SetTime(_sellTime, _now.AddSeconds(30));
            }
            _logger.LogInformation($"Detected change in HNS: {e.NewAmount - e.PreviousAmount:N6}");
        }

        /**
         * Cancels all open orders and reconciles internal state
         */
        public async Task Reset()
        {
            _logger.LogCritical("Starting!");
            Console.SetCursorPosition(0, _config.NumberOrders * 2 + 1 /*header*/ + 1 /*center*/ + 9 /*box*/);
            _logs.Render();

            Console.SetCursorPosition(0, 0);
            Console.WriteLine("Reset bot started");
            _buys.Clear();
            _sells.Clear();

            var orders = await _client.GetExistingOrders(filter: order =>
                order.Status == OrderStatus.NEW || order.Status == OrderStatus.PARTIALLY_FILLED);

            var operations = new List<Task>();
            foreach (var order in orders)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                _logger.LogInformation($"Submit pending cancel order {order.OrderId}");
                operations.Add(_client.CancelOrder(order.OrderId));
            }

            Console.ResetColor();
            Console.Write("Waiting for pending operations...");
            await Task.WhenAll(operations);
            Console.WriteLine("Done!");
        }

        private bool AlreadyUpdating;

        public async Task MaybeUpdate()
        {
            if (!AlreadyUpdating)
            {
                AlreadyUpdating = true;
                await Task.WhenAll(CreateBuySide(), CreateSellSide());
                AlreadyUpdating = false;
            } else
            {
                _logger.LogWarning("Detected an update while already updating, maybe you have your update period set too low?");
                _logger.LogWarning("This is normal while starting up or creating a bunch of orders.");
            }
        }

        public Task CreateBuySide()
        {
            _logger.LogInformation($"Evaluating buy command: {_buyCommand} with scheduled execution at {_buyTime}");
            switch (_buyCommand)
            {
                default:
                case { } c when c == Command.None:
                case { } x when x == Command.DelayedUpdate && _now < _buyTime:
                    return Task.CompletedTask;
                case { } c when c == Command.PriorityUpdate:
                case { } x when x == Command.DelayedUpdate && _now >= _buyTime:
                    _buyPoint = _center.BuySide;
                    _buyCommand = Command.None;
                    return _account.Btc.Total > _config.BtcZero
                        ? UpdateSpread(_buyPoint, _buyHeavy, _buys)
                        : Task.CompletedTask;
            }
        }

        public Task CreateSellSide()
        {
            _logger.LogInformation($"Evaluating buy command: {_sellCommand} with scheduled execution at {_sellTime}");
            switch (_sellCommand)
            {
                default:
                case { } c when c == Command.None:
                case { } x when x == Command.DelayedUpdate && _now < _sellTime:
                    return Task.CompletedTask;
                case { } c when c == Command.PriorityUpdate:
                case { } x when x == Command.DelayedUpdate && _now >= _sellTime:
                    _sellPoint = _center.SellSide;
                    _sellCommand = Command.None;
                    return _account.Hns.Total > _config.HnsZero
                        ? UpdateSpread(_sellPoint, _sellHeavy, _sells)
                        : Task.CompletedTask;
            }
        }

        private async Task UpdateSpread(CeilingData ceiling, Client.Heavy heavy, List<ObservableOrder> orders)
        {
            var spread = Math.Abs(ceiling.Ceiling - ceiling.Bottom) / _config.NumberOrders;
            if (spread == 0m) return;

            if (orders.Count < _config.NumberOrders)
            {
                var newOrders = await CreateSpread(ceiling, heavy, orders);
                orders.AddRange(newOrders.Select(order =>
                {
                    var o = new ObservableOrder(order, _client, _logger);
                    o.StatusChanged += OrderOnStatusChanged;
                    return o;
                }));
                return;
            } else if(orders.Count > _config.NumberOrders)
            {
                _logger.LogError("Extra orders detected!");
            }

            var pendingUpdates = new List<(decimal Price, decimal Quantity)>();

            var balance = orders == _sells
                ? _account.Hns.Total - _config.HnsZero
                : _account.Btc.Total - _config.BtcZero;
            balance *= orders == _sells ? _config.HnsRisk : _config.BtcRisk;

            for (var i = 0; i < _config.NumberOrders; i++)
            {
                var bid = 0m;
                switch (heavy)
                {
                    case Client.Heavy.None:
                        bid = balance / _config.NumberOrders;
                        break;
                    case Client.Heavy.Bottom:
                        bid = (0.1m * i + 1m) / 55 * balance;
                        break;
                    case Client.Heavy.Top:
                        bid = (0.1m * (_config.NumberOrders - i) + 1m) / 55m * balance;
                        break;
                }

                var price = (ceiling.Ceiling - ceiling.Bottom) / _config.NumberOrders;
                price = ceiling.Bottom + (orders == _sells ? 1m : -1m) * _config.MinDistanceFromCenter + price * i;

                if (Math.Abs(price - ceiling.Bottom) < _config.MinDistanceFromCenter)
                    price = orders == _sells
                        ? price + _config.MinDistanceFromCenter
                        : price - _config.MinDistanceFromCenter;

                if (orders == _buys) bid = Client.ConvertBtcToHns(bid, price);

                pendingUpdates.Add((price, bid));
            }

            var tasks = pendingUpdates.Select((x, index) =>
            {
                if (orders.Count <= index) return Task.CompletedTask;

                if (orders[index].Price != x.Price || orders[index].Quantity != x.Quantity)
                    return orders[index].Update(x.Quantity, x.Price);

                return Task.CompletedTask;
            });

            _logger.LogWarning($"{pendingUpdates.Count} updates!");

            await Task.WhenAll(tasks);
        }

        private void OrderOnStatusChanged(object sender, ObservableOrder.StatusUpdateEventArgs e)
        {
             _logger.LogInformation($"Detected order status change from {e.PreviousStatus} to {e.NewStatus}");

            switch (e.NewStatus)
            {
                case OrderStatus.FILLED:
                case OrderStatus.CLOSED:
                    if (_sells.Contains(sender))
                    {
                        (_sellTime, _sellCommand) = SetTime(_sellTime, _now.AddSeconds(30));
                    }
                    else if (_buys.Contains(sender))
                    {
                        (_buyTime, _buyCommand) = SetTime(_buyTime, _now.AddSeconds(30));
                    }

                    break;
                case OrderStatus.PARTIALLY_FILLED:
                    if(_sells.Contains(sender) && _sellCommand == Command.DelayedUpdate)
                    {
                        (_sellTime, _sellCommand) = SetTime(_sellTime, _now.AddSeconds(30));
                    } else if(_buys.Contains(sender) && _buyCommand == Command.DelayedUpdate)
                    {
                        (_buyTime, _buyCommand) = SetTime(_buyTime, _now.AddSeconds(30));
                    }

                    break;
                default:
                    return;
            }
        }

        private async Task<List<Order>> CreateSpread(CeilingData ceiling, Client.Heavy heavy,
            List<ObservableOrder> orders)
        {
            var spread = Math.Abs(ceiling.Ceiling - ceiling.Bottom) / _config.NumberOrders;
            if (spread == 0m) return new List<Order>();

            var numberOrdersToCreate = _config.NumberOrders - orders.Count;
            if (numberOrdersToCreate <= 0) return new List<Order>();

            var balance = orders == _sells
                ? _account.Hns.Total - _config.HnsZero
                : _account.Btc.Total - _config.BtcZero;
            balance *= orders == _sells ? _config.HnsRisk : _config.BtcRisk;

            var operations = new List<Task<Order>>();

            for (var i = 0; i < numberOrdersToCreate; i++)
            {
                var bid = 0m;
                switch (heavy)
                {
                    case Client.Heavy.None:
                        bid = balance / _config.NumberOrders;
                        break;
                    case Client.Heavy.Bottom:
                        bid = (0.1m * i + 1m) / 55 * balance;
                        break;
                    case Client.Heavy.Top:
                        bid = (0.1m * (_config.NumberOrders - i) + 1m) / 55m * balance;
                        break;
                }

                var price = (ceiling.Ceiling - ceiling.Bottom) / _config.NumberOrders;
                price = ceiling.Bottom + (orders == _sells ? 1m : -1m) * _config.MinDistanceFromCenter + price * i;

                if (Math.Abs(price - ceiling.Bottom) < _config.MinDistanceFromCenter)
                    price = orders == _sells
                        ? price + _config.MinDistanceFromCenter
                        : price - _config.MinDistanceFromCenter;

                if (orders == _buys) bid = Client.ConvertBtcToHns(bid, price);

                var order = new SendOrder
                {
                    Price = price.ToString(),
                    Side = orders == _sells ? OrderSide.SELL : OrderSide.BUY,
                    Quantity = bid.ToString(),
                    Timestamp = DateTime.UtcNow.ToUnixTime(),
                    Type = OrderType.LMT
                };

                operations.Add(_client.CreateOrder(order));
                _logger.LogInformation($"{(orders == _sells ? "Sell" : "Buy")} order placed for {price} at {bid:F8}");
            }

            return (await Task.WhenAll(operations)).ToList();
        }

        public async Task Display()
        {
            var balance = new
            {
                Btc = new TrackableValue(),
                Hns = new TrackableValue()
            };

            _sellCommand = _buyCommand = Command.PriorityUpdate;
            Console.OutputEncoding = Encoding.UTF8;

            if(!File.Exists("balances.csv"))
            {
                await File.WriteAllLinesAsync("balances.csv", new string[] {"time,hns,btc,btc value,total"});
            }

            var minOrder = (await _client.GetExistingOrders()).Max(x => x.OrderId);

            while (true)
            {
                _now = DateTime.Now;
                //var orders = await _client.GetExistingOrders(filter: (order =>
                //    order.Status == OrderStatus.PARTIALLY_FILLED || order.Status == OrderStatus.NEW), minOrderId: minOrder);
                await Task.WhenAll(_sells.Select(x => x.Update()).Concat(_buys.Select(x => x.Update()))
                    .Concat(new[] {_center.Update(), _account.Update()}));
                Console.SetCursorPosition(0, 0);
                ConsoleBox.WriteLine($"{_now} local / {_now.ToUniversalTime()} UTC");

                await File.AppendAllLinesAsync("balances.csv",
                    new[]
                    {
                        $"{_now.ToUniversalTime()},{_account.Hns.Total},{_account.Btc.Total},{Client.ConvertBtcToHns(_account.Btc.Total, _center.SellSide.Bottom)},{Client.ConvertBtcToHns(_account.Btc.Total, _center.SellSide.Bottom) + _account.Hns.Total}"
                    });

                var lines = new Lines();
                var predictBuy = _center.PredictBuy(_now.AddSeconds(60));
                var predictSell = _center.PredictSale(_now.AddSeconds(60));
                //lines.OrderChart(orders, _sellPoint, _buyPoint, predictSell, predictBuy);
                var potentialBalance = lines.OrderChart(_sells.Concat(_buys).Select(x => x.RawOrder), _sellPoint,
                    _buyPoint, predictSell, predictBuy);

                balance.Hns.LatestValue = _account.Hns.Total;
                balance.Btc.LatestValue = _account.Btc.Total;

                lines.Box(
                    lines.CurrentBalanceLine(balance.Hns, "HNS"),
                    lines.CurrentBalanceLine(balance.Btc, "BTC",
                        Client.ConvertBtcToHns(balance.Btc.LatestValue, _center.SellSide.Bottom),
                        Client.ConvertBtcToHns(balance.Btc.LatestValue, _center.SellSide.Bottom) +
                        balance.Hns.LatestValue),
                    lines.CenterLine(_center.SellSide, _sellPoint, OrderSide.SELL, 0),
                    lines.CenterLine(_center.BuySide, _buyPoint, OrderSide.BUY, 0),
                    $"| Predict sell +5s: {_center.PredictSale(_now.AddSeconds(5)):N8} +10s {_center.PredictSale(_now.AddSeconds(10)):N8} +60s {_center.PredictSale(_now.AddMinutes(1)):N8}",
                    $"| Predict buy  +5s: {_center.PredictBuy(_now.AddSeconds(5)):N8} +10s {_center.PredictBuy(_now.AddSeconds(10)):N8} +60s {_center.PredictBuy(_now.AddMinutes(1)):N8}",
                    $"| Commands: [buy - {_buyCommand}] [sell - {_sellCommand}]"
                );

                var timer = Task.Delay(TimeSpan.FromSeconds(_config.UpdatePeriod));
                await Task.WhenAll(timer, MaybeUpdate());
            }
        }

        public struct Configuration
        {
            public string BotName { get; }
            public decimal BtcZero { get; set; }
            public decimal HnsZero { get; set; }
            public int NumberOrders { get; set; }
            public decimal MinDistanceFromCenter { get; set; }
            public decimal BtcRisk { get; set; }
            public decimal HnsRisk { get; set; }
            public decimal SellBottomChange { get; set; }
            public decimal SellTopChange { get; set; }
            public double UpdatePeriod { get; set; }
            public decimal BtcRatio { get; set; }
            public decimal HnsRatio { get; set; }

            public Configuration(string botName)
            {
                BotName = botName;
                HnsZero = BtcZero = 0m;
                NumberOrders = 10;
                MinDistanceFromCenter = 0.00000005m;
                BtcRisk = HnsRisk = 0.5m;
                SellBottomChange = 10;
                SellTopChange = 10;
                UpdatePeriod = 5;
                BtcRatio = 0.5m;
                HnsRatio = 0.5m;
            }
        }
    }
}