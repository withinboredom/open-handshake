using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.NamebaseClient;
using Bot.NamebaseClient.Requests;
using Bot.NamebaseClient.Responses;
using Microsoft.Extensions.Logging;

namespace Bot.Brains
{
    class TrendingDown : GridBrain
    {
        public TrendingDown(Client client, ObservableAccount account, ObservableCenterPoint center, TradingBot.Configuration config, ILogger logger, Func<SendOrder, List<ObservableOrder>, Task> createOrder, List<ObservableOrder> sells, List<ObservableOrder> buys) : base(client, account, center, config, logger, createOrder, sells, buys)
        {
            _sellType = SellType.Limit;
        }

        private enum SellType
        {
            Limit,
            Market
        }

        private SellType _sellType;
        private bool _hold;

        public override void TrendUpdate(TracableValue newTrend)
        {
            if (Now - SellPoint.Timestamp > TimeSpan.FromMinutes(5))
            {
                _sellType = SellType.Market;
                (_sellTime, ExecutingSellCommand) = SetTime(_sellTime, ExecutingSellCommand, Now);
            }
            
            if (newTrend.LatestValue < -0.8m)
            {
                (_sellTime, ExecutingSellCommand) = SetTime(_sellTime, ExecutingSellCommand, Now + TimeSpan.FromSeconds(120));
            }
            else if (newTrend.LatestValue > 0.1m && ExecutingSellCommand == TradingBot.Command.DelayedUpdate)
            {
                ExecutingSellCommand = TradingBot.Command.None;
            }
        }

        public override Task CreateSellSide()
        {
            if (_hold)
            {
                return base.CreateSellSide();
            }
            _logger.LogInformation($"Evaluating buy command: {ExecutingSellCommand} with scheduled execution at {_sellTime}");
            switch (ExecutingSellCommand)
            {
                default:
                case { } c when c == TradingBot.Command.None:
                case { } x when x == TradingBot.Command.DelayedUpdate && Now < _sellTime:
                    return Task.CompletedTask;
                case TradingBot.Command.PriceUpdate:
                    ExecutingSellCommand = TradingBot.Command.None;
                    return _sells[0].Update((_account.Hns.Total - _config.HnsZero) * _config.HnsRisk, _sells[0].Price);
                case { } c when c == TradingBot.Command.PriorityUpdate:
                case { } x when x == TradingBot.Command.DelayedUpdate && Now >= _sellTime:
                    SellPoint = _center.SellSide;
                    ExecutingSellCommand = TradingBot.Command.None;
                    return _account.Hns.Total > _config.HnsZero
                        ? PlaceSellOrder((_account.Hns.Total - _config.HnsZero) * _config.HnsRisk)
                        : Task.CompletedTask;
            }
        }

        protected override void Announce(string message)
        {
            _logger.LogInformation("Trend down brain ");
        }

        private async Task PlaceSellOrder(decimal value)
        {
            var operations = new List<Task>();

            foreach (var order in _sells)
            {
                operations.Add(order.Update(0, 0));
            }

            await Task.WhenAll(operations);

            var conversion = _center.SellSide.Bottom + _config.MinDistanceFromCenter;
            if (_sells.Count > 1)
            {
                if (!_hold)
                {
                    _hold = _sellType == SellType.Market || _hold;
                    await _sells[0].Update(value, conversion,
                        _sellType == SellType.Limit ? OrderType.LMT : OrderType.MKT);
                }
            }
            else
            {
                await CreateOrder(new SendOrder
                {
                    Price = conversion.ToString(),
                    Quantity = value.ToString(),
                    Side = OrderSide.SELL,
                    Type = OrderType.LMT,
                }, _sells);
            }
        }
    }
}