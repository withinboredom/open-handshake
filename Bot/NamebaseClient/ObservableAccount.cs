using System;
using System.Threading.Tasks;
using Bot.NamebaseClient.Responses;

namespace Bot.NamebaseClient
{
    public class ObservableAccount
    {
        public delegate void BtcUpdatedHandler(object sender, BalanceUpdatedEventArgs e);

        public delegate void HnsUpdatedHandler(object sender, BalanceUpdatedEventArgs e);

        private readonly Client _client;
        private Account _account;

        public ObservableAccount(Client client, Account account)
        {
            _client = client;
            _account = account;
        }

        public AccountBalance Hns => _account.Hns;
        public AccountBalance Btc => _account.Btc;

        public async Task Update()
        {
            var previous = _account;
            _account = await _client.GetAccount();

            if (previous.Btc.Total != _account.Btc.Total)
                BtcUpdated?.Invoke(this, new BalanceUpdatedEventArgs
                {
                    NewAmount = _account.Btc.Total,
                    PreviousAmount = previous.Btc.Total
                });

            if (previous.Hns.Total != _account.Hns.Total)
                HnsUpdated?.Invoke(this, new BalanceUpdatedEventArgs
                {
                    NewAmount = _account.Hns.Total,
                    PreviousAmount = previous.Hns.Total
                });
        }

        public event HnsUpdatedHandler? HnsUpdated;
        public event BtcUpdatedHandler? BtcUpdated;


        public class BalanceUpdatedEventArgs : EventArgs
        {
            public decimal PreviousAmount { get; set; }
            public decimal NewAmount { get; set; }
        }
    }
}