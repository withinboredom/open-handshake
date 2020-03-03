using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.NamebaseClient
{
    public class ObservableCenterPoint
    {
        public delegate void HandleChangedBuyCeiling(object sender, CeilingChangedEventArgs e);

        public delegate void HandleChangedSellCeiling(object sender, CeilingChangedEventArgs e);

        private readonly Client _client;

        private TracableValue _buyTrace;
        private TracableValue _sellTrace;

        public Dictionary<decimal, TracableValue> BuyResistanceLife;
        public Dictionary<decimal, TracableValue> SellResistanceLife;

        public OrderBook OrderBook;
        
        public CeilingData BuySide;
        public Func<DateTime, decimal> PredictBuy;

        public Func<DateTime, decimal> PredictSale;
        public CeilingData SellSide;

        public ObservableCenterPoint(Client client, (CeilingData, CeilingData, OrderBook) data)
        {
            _client = client;
            (BuySide, SellSide, OrderBook) = data;
            _sellTrace = new TracableValue(120);
            _buyTrace = new TracableValue(120);
            BuyResistanceLife = new Dictionary<decimal, TracableValue>();
            SellResistanceLife = new Dictionary<decimal, TracableValue>();
        }

        private void UpdateSellTrace()
        {
            _sellTrace.LatestValue = SellSide.Bottom;
            (_, PredictSale, _) = _sellTrace.Predict();
            SyncResistance(SellSide, SellResistanceLife);
        }

        private void SyncResistance(CeilingData side, Dictionary<decimal, TracableValue> lifetime)
        {
            foreach (var (level, totalAmount) in side.Resistance)
            {
                if (lifetime.ContainsKey(level))
                {
                    var value = lifetime[level];
                    value.LatestValue = totalAmount;
                    lifetime[level] = value;
                }
                else
                {
                    lifetime[level] = new TracableValue(120) { LatestValue = totalAmount };
                }
            }

            var keysToRemove = new List<decimal>();
            foreach (var (key, value) in lifetime)
            {
                if (side.Resistance.All(x => x.Level != key))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                SellResistanceLife.Remove(key);
            }
        }

        private void UpdateBuyTrace()
        {
            _buyTrace.LatestValue = BuySide.Bottom;
            (_, PredictBuy, _) = _buyTrace.Predict();
            SyncResistance(BuySide, BuyResistanceLife);
        }

        public event HandleChangedBuyCeiling? BuyCeilingChanged;
        public event HandleChangedSellCeiling? SellCeilingChanged;

        public async Task Update()
        {
            var previous = (BuySide, SellSide);
            (BuySide, SellSide, OrderBook) = await _client.GetCenterPoint();
            UpdateBuyTrace();
            UpdateSellTrace();

            if (BuySide.Resistance[0].Level != previous.BuySide.Resistance[0].Level || BuySide.Bottom != previous.BuySide.Bottom)
                BuyCeilingChanged?.Invoke(this, new CeilingChangedEventArgs
                {
                    New = BuySide,
                    Previous = previous.BuySide
                });

            if (SellSide.Resistance[0].Level != previous.SellSide.Resistance[0].Level || SellSide.Bottom != previous.SellSide.Bottom)
                SellCeilingChanged?.Invoke(this, new CeilingChangedEventArgs
                {
                    Previous = previous.SellSide,
                    New = SellSide
                });
        }

        public class CeilingChangedEventArgs : EventArgs
        {
            public CeilingData Previous { get; set; }
            public CeilingData New { get; set; }
        }
    }
}