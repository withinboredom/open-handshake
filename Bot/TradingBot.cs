using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Brains;
using Bot.NamebaseClient;
using Bot.NamebaseClient.Requests;
using Bot.NamebaseClient.Responses;
using Microsoft.Extensions.Logging;

namespace Bot
{
    /// <summary>
    /// A trading bot
    /// </summary>
    public class TradingBot
    {
        /// <summary>
        /// The account
        /// </summary>
        private readonly ObservableAccount _account;

        /// <summary>
        /// The buys
        /// </summary>
        private readonly List<ObservableOrder> _buys;

        /// <summary>
        /// The center
        /// </summary>
        private readonly ObservableCenterPoint _center;

        /// <summary>
        /// The client
        /// </summary>
        private readonly Client _client;

        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        public Configuration _config { get; set; }

        /// <summary>
        /// The logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// The sells
        /// </summary>
        private readonly List<ObservableOrder> _sells;

        /// <summary>
        /// The logs
        /// </summary>
        private readonly ConsoleBox _logs = new ConsoleBox("Logs");

        /// <summary>
        /// The state box
        /// </summary>
        private readonly ConsoleBox _stateBox = new ConsoleBox("Bot Status");

        /// <summary>
        /// The brain
        /// </summary>
        private IBrain _brain;

        /// <summary>
        /// Available commands to the bot
        /// </summary>
        public enum Command
        {
            None,
            PriorityUpdate,
            DelayedUpdate,
        }

        /// <summary>
        /// The type of trend detected
        /// </summary>
        public enum Trend
        {
            Random,
            Up,
            Down,
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TradingBot"/> class.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="account">The account.</param>
        /// <param name="center">The center.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="logger">The logger.</param>
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
            //_brain = new GridBrain(client, account, center, config, logger, CreateOrder, _sells, _buys);
            _brain = new GridBrain(client, account, center, config, logger, CreateOrder, _sells, _buys);
            _brain.Now = DateTime.Now;

            ListLogger.Stream.CollectionChanged += OnNewLogs;
        }

        private async Task CreateOrder(SendOrder sendOrder, List<ObservableOrder> orders)
        {
            var order = await _client.CreateOrder(sendOrder);
            var observableOrder = new ObservableOrder(order, _client, _logger);
            observableOrder.StatusChanged += OrderOnStatusChanged;
            orders.Add(observableOrder);
        }

        /// <summary>
        /// Streams the on collection changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs"/> instance containing the event data.</param>
        private void OnNewLogs(object sender, NotifyCollectionChangedEventArgs e)
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

        /// <summary>
        /// Centers the on sell ceiling changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ObservableCenterPoint.CeilingChangedEventArgs"/> instance containing the event data.</param>
        private void CenterOnSellCeilingChanged(object sender, ObservableCenterPoint.CeilingChangedEventArgs e)
        {
            if(_spikeSignal.LatestValue == 0)
                _brain.SellCeilingChanged(sender as ObservableCenterPoint, e);
        }

        /// <summary>
        /// Centers the on buy ceiling changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ObservableCenterPoint.CeilingChangedEventArgs"/> instance containing the event data.</param>
        private void CenterOnBuyCeilingChanged(object sender, ObservableCenterPoint.CeilingChangedEventArgs e)
        {
            if(_spikeSignal.LatestValue == 0)
                _brain.BuyCeilingChanged(sender as ObservableCenterPoint, e);
        }

        /// <summary>
        /// Creates the instance.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="logger">The logger.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Triggers an update when the balance changes
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ObservableAccount.BalanceUpdatedEventArgs"/> instance containing the event data.</param>
        private void AccountOnBtcUpdated(object sender, ObservableAccount.BalanceUpdatedEventArgs e)
        {
            _brain.BtcUpdated(sender as ObservableAccount, e);
        }

        /// <summary>
        /// Accounts the on HNS updated.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ObservableAccount.BalanceUpdatedEventArgs"/> instance containing the event data.</param>
        private void AccountOnHnsUpdated(object sender, ObservableAccount.BalanceUpdatedEventArgs e)
        {
            _brain.HnsUpdated(sender as ObservableAccount, e);
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public async Task Reset()
        {
            _logger.LogCritical("Starting!");
            Console.SetCursorPosition(0, _config.NumberOrders * 2 + 1 /*header*/ + 1 /*center*/ + 10 /*box*/);
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

        /// <summary>
        /// Orders the on status changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="Bot.NamebaseClient.ObservableOrder.StatusUpdateEventArgs" /> instance containing the event data.</param>
        private void OrderOnStatusChanged(object sender, ObservableOrder.StatusUpdateEventArgs e)
        {
            _brain.OrderStatusChanged(sender as ObservableOrder, e);
        }

        private Trend _trend = Trend.Random;
        private TracableValue _midpoint = new TracableValue(600);
        private TracableValue _acceleration = new TracableValue(600);
        private TracableValue _spikeSignal = new TracableValue(600);

        private void DetectTrend()
        {
            _midpoint.LatestValue = (((_center.SellSide.Bottom - _center.BuySide.Bottom) / 2m) + _center.BuySide.Bottom) * 1000000000000m;
            //var (slope, _, _) = _midpoint.Predict();
            //_acceleration.LatestValue = slope;

            //(slope, _, _) = _acceleration.Predict();
            (_spikeSignal.LatestValue, _acceleration.LatestValue) = _midpoint.Signal(30, 4.5m, 0.1m);

            if (_acceleration.LatestValue > 0)
            {
                _trend = Trend.Up;
            }
            else if (_acceleration.LatestValue < 0)
            {
                _trend = Trend.Down;
            }
            else
            {
                _trend = Trend.Random;
            }
        }

        /// <summary>
        /// Displays this instance.
        /// </summary>
        /// <returns></returns>
        public async Task Display()
        {
            var balance = new
            {
                Btc = new TrackableValue(),
                Hns = new TrackableValue()
            };

            _brain.ExecutingSellCommand = _brain.ExecutingBuyCommand = Command.PriorityUpdate;
            Console.OutputEncoding = Encoding.UTF8;

            if (!File.Exists("balances.csv"))
            {
                await File.WriteAllLinesAsync("balances.csv", new string[] { "time,hns,btc,btc value,total,btc conversion rate" });
            }

            _logs.Top = _config.NumberOrders * 2 + 1 /*header*/ + 1 /*center*/ + 11;
            _stateBox.Top = _config.NumberOrders * 2 + 2;
            _stateBox.MaxLines = 11;
            _stateBox.Render();

            while (true)
            {
                Console.ForegroundColor = Console.BackgroundColor == ConsoleColor.Black
                    ? ConsoleColor.White
                    : ConsoleColor.Black;
                _brain.Now = DateTime.Now;
                DetectTrend();

                if (_trend == Trend.Random && !(_brain is GridBrain))
                {
                    _brain = new GridBrain(_client, _account, _center, _config, _logger, CreateOrder, _sells, _buys);
                    _brain.ExecutingSellCommand = Command.PriorityUpdate;
                    _brain.ExecutingBuyCommand = Command.PriorityUpdate;
                }
                else if (_trend == Trend.Up && !(_brain is TrendingUp))
                {
                    _brain = new TrendingUp(_client, _account, _center, _config, _logger, CreateOrder, _sells, _buys);
                    _brain.ExecutingSellCommand = Command.PriorityUpdate;
                    _brain.ExecutingBuyCommand = Command.PriorityUpdate;
                }
                else if (_trend == Trend.Down && !(_brain is GridBrain))
                {
                    _brain = new GridBrain(_client, _account, _center, _config, _logger, CreateOrder, _sells, _buys);
                    _brain.ExecutingSellCommand = Command.PriorityUpdate;
                    _brain.ExecutingBuyCommand = Command.PriorityUpdate;
                }

                await Task.WhenAll(_sells.Select(x => x.Update()).Concat(_buys.Select(x => x.Update()))
                    .Concat(new[] { _center.Update(), _account.Update() }));
                Console.SetCursorPosition(0, 0);
                ConsoleBox.WriteLine($"{_brain.Now} local / {_brain.Now.ToUniversalTime()} UTC");

                //var data = string.Join(',', _midpoint.Values.Select(x => x.Value.ToString()));

                await File.AppendAllLinesAsync("balances.csv",
                    new[]
                    {
                        $"{_brain.Now.ToUniversalTime()},{_account.Hns.Total},{_account.Btc.Total},{Client.ConvertBtcToHns(_account.Btc.Total, _center.BuySide.Bottom)},{Client.ConvertBtcToHns(_account.Btc.Total, _center.BuySide.Bottom) + _account.Hns.Total},{_center.BuySide.Bottom}"
                    });

                var lines = new Lines();

                balance.Hns.LatestValue = _account.Hns.Total;
                balance.Btc.LatestValue = _account.Btc.Total;

                var sellPredictor = _center.SellResistanceLife[_center.SellResistanceLife.Keys.Min()].Predict();
                var buyPredictor = _center.BuyResistanceLife[_center.BuyResistanceLife.Keys.Max()].Predict();

                var breaking = (Sell: sellPredictor.PredictY(0), Buy: buyPredictor.PredictY(0));
                var btcToHns = _center.OrderBook.SellBtc(balance.Btc.LatestValue);
                var hnsToBtc = _center.OrderBook.SellHns(balance.Hns.LatestValue);

                lines.OrderChart(
                    _buys.Concat(_sells),
                    _brain.SellPoint,
                    _brain.BuyPoint,
                    _center.PredictSale(_brain.Now + TimeSpan.FromMinutes(1)),
                    _center.PredictBuy(_brain.Now + TimeSpan.FromMinutes(1)));

                var box = new [] {
                    lines.CurrentBalanceLine(balance.Hns, "HNS", hnsToBtc.Btc, hnsToBtc.Btc + balance.Btc.LatestValue) + $" brings to level: {hnsToBtc.ToLevel:N8}",
                    lines.CurrentBalanceLine(balance.Btc, "BTC", btcToHns.Hns, btcToHns.Hns + balance.Hns.LatestValue) + $" brings to level: {btcToHns.ToLevel:N8}",
                    lines.CenterLine(_center.SellSide, _brain.SellPoint, OrderSide.SELL, 0),
                    lines.CenterLine(_center.BuySide, _brain.BuyPoint, OrderSide.BUY, 0),
                    $"Predict sell +5s: {_center.PredictSale(_brain.Now.AddSeconds(5)):N8} +10s {_center.PredictSale(_brain.Now.AddSeconds(10)):N8} +60s {_center.PredictSale(_brain.Now.AddMinutes(1)):N8}",
                    $"Passing resistance at {(breaking.Sell < DateTime.Now || breaking.Sell == DateTime.MaxValue ? "never" : breaking.Sell.ToString())} strength: {_center.SellResistanceLife[_center.SellResistanceLife.Keys.Min()].LatestValue:N6}",
                    $"Predict buy  +5s: {_center.PredictBuy(_brain.Now.AddSeconds(5)):N8} +10s {_center.PredictBuy(_brain.Now.AddSeconds(10)):N8} +60s {_center.PredictBuy(_brain.Now.AddMinutes(1)):N8}",
                    $"Passing resistance at {(breaking.Buy < DateTime.Now || breaking.Buy == DateTime.MaxValue ? "never" : breaking.Buy.ToString())} strength: {_center.BuyResistanceLife[_center.BuyResistanceLife.Keys.Min()].LatestValue:N6}",
                    $"Commands: [buy - {_brain.ExecutingBuyCommand}] [sell - {_brain.ExecutingSellCommand}]",
                    $"Trend: {_midpoint.LatestValue} -> {_acceleration.LatestValue} -> {_acceleration.Predict().Slope} :: {_spikeSignal.LatestValue} / {_trend}",
                };

                _stateBox.Update(box);

                var timer = Task.Delay(TimeSpan.FromSeconds(_config.UpdatePeriod));
                await Task.WhenAll(timer, _brain.MaybeUpdate());
            }
        }

        /// <summary>
        /// The bot configuration
        /// </summary>
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