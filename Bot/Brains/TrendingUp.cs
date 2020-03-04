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
    /// <summary>
    /// This brain handles up trends. It uses a grid to make money on down bursts, but tries to keep liquidity in the
    /// appreciating currency.
    /// </summary>
    /// <seealso cref="Bot.Brains.IBrain" />
    class TrendingUp : GridBrain
    {
        public TrendingUp(
            Client client,
            ObservableAccount account,
            ObservableCenterPoint center,
            TradingBot.Configuration config,
            ILogger logger,
            Func<SendOrder, List<ObservableOrder>, Task> createOrder,
            List<ObservableOrder> sells,
            List<ObservableOrder> buys
        ) : base(client, account, center, config, logger, createOrder, sells, buys)
        {
        }

        /// <inheritdoc />
        public override void TrendUpdate(TracableValue newTrend)
        {
            if (newTrend.LatestValue > 0.8m)
            {
                (_buyTime, ExecutingBuyCommand) = SetTime(_buyTime, Now + TimeSpan.FromSeconds(120));
            } else if (newTrend.LatestValue < 0.1m && ExecutingBuyCommand == TradingBot.Command.DelayedUpdate)
            {
                ExecutingBuyCommand = TradingBot.Command.None;
            }
        }

        public override Task CreateBuySide()
        {
            _logger.LogInformation($"Evaluating trend buy command: {ExecutingBuyCommand} with scheduled execution at {_buyTime}");
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
                        ? PlaceBuyOrder((_account.Btc.Total - _config.BtcZero) * _config.BtcRisk) // UpdateSpread(BuyPoint, _buyHeavy, _buys, index)
                        : Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        protected override void Announce(string message)
        {
            base.Announce("Up trend brain " + message);
        }

        private async Task PlaceBuyOrder(decimal value)
        {
            var operations = new List<Task>();

            foreach (var order in _buys)
            {
                operations.Add(order.Update(0, 0));
            }

            await Task.WhenAll(operations);

            var conversion = _center.BuySide.Bottom - _config.MinDistanceFromCenter;

            if(_buys.Count > 1)
                await _buys[0].Update(Client.ConvertBtcToHns(value, conversion), conversion);
            else
            {
                await CreateOrder(new SendOrder
                {
                    Price = conversion.ToString(),
                    Quantity = Client.ConvertBtcToHns(value, conversion).ToString(),
                    Side = OrderSide.BUY,
                    Type = OrderType.LMT,
                }, _buys);
                BuyPoint = _center.BuySide;
                SellPoint = _center.SellSide;
            }
        }
    }
}
