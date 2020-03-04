using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.NamebaseClient;
using Bot.NamebaseClient.Requests;
using Bot.NamebaseClient.Responses;
using Microsoft.Extensions.Logging;

namespace Bot.Brains
{
    class GridBrain : IBrain
    {
        protected readonly Client _client;
        protected readonly ObservableAccount _account;
        protected readonly ObservableCenterPoint _center;
        protected readonly TradingBot.Configuration _config;
        protected readonly ILogger _logger;

        public DateTime Now { get; set; }

        /// <inheritdoc />
        public CeilingData SellPoint { get; set; }

        /// <inheritdoc />
        public CeilingData BuyPoint { get; set; }

        protected DateTime _buyTime;
        protected DateTime _sellTime;
        protected Client.Heavy _buyHeavy;
        protected Client.Heavy _sellHeavy;
        protected List<ObservableOrder> _sells;
        protected List<ObservableOrder> _buys;
        protected Func<SendOrder, List<ObservableOrder>, Task> CreateOrder;

        public GridBrain(Client client, ObservableAccount account, ObservableCenterPoint center, TradingBot.Configuration config, ILogger logger, Func<SendOrder, List<ObservableOrder>, Task> createOrder, List<ObservableOrder> sells, List<ObservableOrder> buys)
        {
            _client = client;
            _account = account;
            _center = center;
            _config = config;
            _logger = logger;
            _sells = sells;
            _buys = buys;
            CreateOrder = createOrder;

            Now = DateTime.Now;
            _buyHeavy = Client.Heavy.None;
            _sellHeavy = Client.Heavy.None;
            _buyTime = _sellTime = Now;
            Announce("turning on");
        }

        protected virtual void Announce(string message)
        {
            _logger.LogInformation("Grid brain " + message);
        }

        ~GridBrain()
        {
            Announce("shutting down");
        }

        protected (DateTime, TradingBot.Command) SetTime(DateTime other, DateTime? set = null)
        {
            if (set == null) set = Now;

            if (set <= Now)
            {
                return (set.Value, TradingBot.Command.PriorityUpdate);
            }

            return (other > set.Value ? other : set.Value, TradingBot.Command.DelayedUpdate);
        }

        /// <inheritdoc />
        public void BtcUpdated(ObservableAccount sender, ObservableAccount.BalanceUpdatedEventArgs e)
        {
            if (Lines.PercentChanged(e.PreviousAmount, e.NewAmount) > 0.15m)
            {
                (_buyTime, ExecutingBuyCommand) = SetTime(_buyTime, Now.AddSeconds(30));
            }
            _logger.LogInformation($"Detected change in BTC: {e.NewAmount - e.PreviousAmount:N8}");
        }

        /// <inheritdoc />
        public void HnsUpdated(ObservableAccount sender, ObservableAccount.BalanceUpdatedEventArgs e)
        {
            if (Lines.PercentChanged(e.PreviousAmount, e.NewAmount) > 0.15m)
            {
                (_sellTime, ExecutingSellCommand) = SetTime(_sellTime, Now.AddSeconds(30));
            }
            _logger.LogInformation($"Detected change in HNS: {e.NewAmount - e.PreviousAmount:N6}");
        }

        /// <inheritdoc />
        public void BuyCeilingChanged(ObservableCenterPoint sender, ObservableCenterPoint.CeilingChangedEventArgs e)
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
                        (_buyTime, ExecutingBuyCommand) = SetTime(_buyTime, Now.AddMinutes(5));
                    }

                    _buyHeavy = Client.Heavy.Bottom;
                }
                else if (curentValueBtc > _config.BtcRatio)
                {
                    if (_buyHeavy != Client.Heavy.Top)
                    {
                        (_buyTime, ExecutingBuyCommand) = SetTime(_buyTime, Now.AddMinutes(5));
                    }

                    _buyHeavy = Client.Heavy.Top;
                }
                else
                {
                    if (_buyHeavy != Client.Heavy.None)
                    {
                        (_buyTime, ExecutingBuyCommand) = SetTime(_buyTime, Now.AddMinutes(5));
                    }

                    _buyHeavy = Client.Heavy.None;
                }
            }

            _logger.LogInformation($"Setting buy priority {_buyHeavy}");

            var percentBottom = Math.Abs(Lines.PercentChanged(BuyPoint?.Bottom ?? 0, e.New.Bottom));
            var percentTop = Math.Abs(Lines.PercentChanged(BuyPoint?.Resistance[0].Level ?? 0, e.New.Resistance[0].Level));

            if (percentBottom <= _config.SellBottomChange && percentTop <= _config.SellTopChange) return;

            (_buyTime, ExecutingBuyCommand) = SetTime(_buyTime, Now);
            _logger.LogInformation($"Major change detected, recalculating buy side order book");
        }

        /// <inheritdoc />
        public void SellCeilingChanged(ObservableCenterPoint sender, ObservableCenterPoint.CeilingChangedEventArgs e)
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
                        (_sellTime, ExecutingSellCommand) = SetTime(_sellTime, Now.AddMinutes(5));
                    }

                    _sellHeavy = Client.Heavy.Bottom;
                }
                else if (currentValueHns > _config.HnsRatio)
                {
                    if (_sellHeavy != Client.Heavy.Top)
                    {
                        (_sellTime, ExecutingSellCommand) = SetTime(_sellTime, Now.AddMinutes(5));
                    }

                    _sellHeavy = Client.Heavy.Top;
                }
                else
                {
                    if (_sellHeavy != Client.Heavy.None)
                    {
                        (_sellTime, ExecutingSellCommand) = SetTime(_sellTime, Now.AddMinutes(5));
                    }

                    _sellHeavy = Client.Heavy.None;
                }
            }

            _logger.LogInformation($"Setting sell priority {_sellHeavy}");

            var percentBottom = Math.Abs(Lines.PercentChanged(SellPoint?.Bottom ?? 0, e.New.Bottom));
            var percentTop = Math.Abs(Lines.PercentChanged(SellPoint?.Resistance[0].Level ?? 0, e.New.Resistance[0].Level));

            if (percentBottom <= _config.SellBottomChange && percentTop <= _config.SellTopChange) return;

            _logger.LogInformation($"Major change detected, recalculating sell side order book");
            (_sellTime, ExecutingSellCommand) = SetTime(_sellTime, Now);
        }

        /// <inheritdoc />
        public void OrderStatusChanged(ObservableOrder sender, ObservableOrder.StatusUpdateEventArgs e)
        {
            _logger.LogInformation($"Detected order status change from {e.PreviousStatus} to {e.NewStatus}");

            switch (e.NewStatus)
            {
                case OrderStatus.FILLED:
                case OrderStatus.CLOSED:
                    if (_sells.Contains(sender))
                    {
                        (_sellTime, ExecutingSellCommand) = SetTime(_sellTime, Now.AddSeconds(30));
                    }
                    else if (_buys.Contains(sender))
                    {
                        (_buyTime, ExecutingBuyCommand) = SetTime(_buyTime, Now.AddSeconds(30));
                    }

                    break;
                case OrderStatus.PARTIALLY_FILLED:
                    if (_sells.Contains(sender) && ExecutingSellCommand == TradingBot.Command.DelayedUpdate)
                    {
                        (_sellTime, ExecutingSellCommand) = SetTime(_sellTime, Now.AddSeconds(30));
                    }
                    else if (_buys.Contains(sender) && ExecutingBuyCommand == TradingBot.Command.DelayedUpdate)
                    {
                        (_buyTime, ExecutingBuyCommand) = SetTime(_buyTime, Now.AddSeconds(30));
                    }

                    break;
                default:
                    return;
            }
        }

        /// <inheritdoc />
        public virtual void TrendUpdate(TracableValue newTrend)
        {
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public TradingBot.Command ExecutingBuyCommand { get; set; }

        /// <inheritdoc />
        public TradingBot.Command ExecutingSellCommand { get; set; }


        /// <summary>
        /// The already updating
        /// </summary>
        protected bool AlreadyUpdating;

        /// <summary>
        /// Maybes the update.
        /// </summary>
        public async Task MaybeUpdate()
        {
            if (!AlreadyUpdating)
            {
                AlreadyUpdating = true;
                await Task.WhenAll(CreateBuySide(), CreateSellSide());
                AlreadyUpdating = false;
            }
            else
            {
                _logger.LogWarning("Detected an update while already updating, maybe you have your update period set too low?");
                _logger.LogWarning("This is normal while starting up or creating a bunch of orders.");
            }
        }

        /// <summary>
        /// Creates the buy side.
        /// </summary>
        /// <returns></returns>
        public virtual Task CreateBuySide()
        {
            _logger.LogInformation($"Evaluating buy command: {ExecutingBuyCommand} with scheduled execution at {_buyTime}");
            switch (ExecutingBuyCommand)
            {
                default:
                case { } c when c == TradingBot.Command.None:
                case { } x when x == TradingBot.Command.DelayedUpdate && Now < _buyTime:
                    return Task.CompletedTask;
                case { } c when c == TradingBot.Command.PriorityUpdate:
                case { } x when x == TradingBot.Command.DelayedUpdate && Now >= _buyTime:
                    var buyingPoint = _center.BuyResistanceLife.Select(x =>
                    {
                        var predict = x.Value.Predict();
                        var time = predict.PredictY(0);
                        return (x.Key, Time: time > Now ? time : DateTime.MaxValue);
                    }).OrderBy(x => x.Time).First();

                    var index = 0;

                    if (buyingPoint.Time != DateTime.MaxValue)
                    {
                        index = _center.BuySide.Resistance.FindIndex(x => x.Level == buyingPoint.Key);
                    }

                    BuyPoint = _center.BuySide;
                    ExecutingBuyCommand = TradingBot.Command.None;
                    return _account.Btc.Total > _config.BtcZero
                        ? UpdateSpread(BuyPoint, _buyHeavy, _buys, index)
                        : Task.CompletedTask;
            }
        }

        /// <summary>
        /// Creates the sell side.
        /// </summary>
        /// <returns></returns>
        public Task CreateSellSide()
        {
            _logger.LogInformation($"Evaluating buy command: {ExecutingSellCommand} with scheduled execution at {_sellTime}");
            switch (ExecutingSellCommand)
            {
                default:
                case { } c when c == TradingBot.Command.None:
                case { } x when x == TradingBot.Command.DelayedUpdate && Now < _sellTime:
                    return Task.CompletedTask;
                case { } c when c == TradingBot.Command.PriorityUpdate:
                case { } x when x == TradingBot.Command.DelayedUpdate && Now >= _sellTime:
                    var sellingPoint = _center.SellResistanceLife.Select(x =>
                    {
                        var predict = x.Value.Predict();
                        var time = predict.PredictY(0);
                        return (x.Key, Time: time > Now ? time : DateTime.MaxValue);
                    }).OrderBy(x => x.Time).First();

                    var sellIndex = 0;

                    if (sellingPoint.Time != DateTime.MaxValue)
                    {
                        sellIndex = _center.SellSide.Resistance.FindIndex(x => x.Level == sellingPoint.Key);
                    }

                    SellPoint = _center.SellSide;
                    ExecutingSellCommand = TradingBot.Command.None;
                    return _account.Hns.Total > _config.HnsZero
                        ? UpdateSpread(SellPoint, _sellHeavy, _sells, sellIndex)
                        : Task.CompletedTask;
            }
        }

        /// <summary>
        /// Updates the spread.
        /// </summary>
        /// <param name="ceiling">The ceiling.</param>
        /// <param name="heavy">The heavy.</param>
        /// <param name="orders">The orders.</param>
        /// <param name="resistanceIndex">Index of the resistance.</param>
        /// <returns></returns>
        protected async Task UpdateSpread(CeilingData ceiling, Client.Heavy heavy, List<ObservableOrder> orders, int resistanceIndex)
        {
            if (resistanceIndex < 0 || resistanceIndex >= ceiling.Resistance.Count)
            {
                _logger.LogCritical("uh oh");
            }
            var spread = Math.Abs(ceiling.Resistance[resistanceIndex].Level - ceiling.Bottom) / _config.NumberOrders;
            if (spread == 0m) return;

            if (orders.Count < _config.NumberOrders)
            {
                await CreateSpread(ceiling, heavy, orders);
                return;
            }

            if (orders.Count > _config.NumberOrders)
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

                var price = (ceiling.Resistance[resistanceIndex].Level - ceiling.Bottom) / _config.NumberOrders;
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

        /// <summary>
        /// Creates the spread.
        /// </summary>
        /// <param name="ceiling">The ceiling.</param>
        /// <param name="heavy">The heavy.</param>
        /// <param name="orders">The orders.</param>
        /// <returns></returns>
        protected async Task CreateSpread(CeilingData ceiling, Client.Heavy heavy,
                    List<ObservableOrder> orders)
        {
            var spread = Math.Abs(ceiling.Resistance[0].Level - ceiling.Bottom) / _config.NumberOrders;
            if (spread == 0m) return;

            var numberOrdersToCreate = _config.NumberOrders - orders.Count;
            if (numberOrdersToCreate <= 0) return;

            var balance = orders == _sells
                ? _account.Hns.Total - _config.HnsZero
                : _account.Btc.Total - _config.BtcZero;
            balance *= orders == _sells ? _config.HnsRisk : _config.BtcRisk;

            var operations = new List<Task>();

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

                var price = (ceiling.Resistance[0].Level - ceiling.Bottom) / _config.NumberOrders;
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

                operations.Add(CreateOrder(order, orders));
                _logger.LogInformation($"{(orders == _sells ? "Sell" : "Buy")} order placed for {price} at {bid:F8}");
            }

            await Task.WhenAll(operations);
        }
    }
}
