using System;
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
        public CeilingData BuySide;
        public Func<DateTime, decimal> PredictBuy;

        public Func<DateTime, decimal> PredictSale;
        public CeilingData SellSide;

        public ObservableCenterPoint(Client client, (CeilingData, CeilingData) data)
        {
            _client = client;
            (BuySide, SellSide) = data;
            _sellTrace = new TracableValue(120);
            _buyTrace = new TracableValue(120);
        }

        private void UpdateSellTrace()
        {
            _sellTrace.LatestValue = SellSide.Bottom;
            (_, PredictSale) = _sellTrace.Predict();
        }

        private void UpdateBuyTrace()
        {
            _buyTrace.LatestValue = BuySide.Bottom;
            (_, PredictBuy) = _buyTrace.Predict();
        }

        public event HandleChangedBuyCeiling? BuyCeilingChanged;
        public event HandleChangedSellCeiling? SellCeilingChanged;

        public async Task Update()
        {
            var previous = (BuySide, SellSide);
            (BuySide, SellSide) = await _client.GetCenterPoint();
            UpdateBuyTrace();
            UpdateSellTrace();

            if (BuySide.Ceiling != previous.BuySide.Ceiling || BuySide.Bottom != previous.BuySide.Bottom)
                BuyCeilingChanged?.Invoke(this, new CeilingChangedEventArgs
                {
                    New = BuySide,
                    Previous = previous.BuySide
                });

            if (SellSide.Ceiling != previous.SellSide.Ceiling || SellSide.Bottom != previous.SellSide.Bottom)
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