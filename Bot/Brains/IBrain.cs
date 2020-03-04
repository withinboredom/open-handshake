using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bot.NamebaseClient;

namespace Bot.Brains
{
    interface IBrain
    {
        /// <summary>
        /// Notification that the btc updated
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ObservableAccount.BalanceUpdatedEventArgs"/> instance containing the event data.</param>
        void BtcUpdated(ObservableAccount sender, ObservableAccount.BalanceUpdatedEventArgs e);

        /// <summary>
        /// Notification that the hns update
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ObservableAccount.BalanceUpdatedEventArgs"/> instance containing the event data.</param>
        void HnsUpdated(ObservableAccount sender, ObservableAccount.BalanceUpdatedEventArgs e);

        /// <summary>
        /// The ceiling changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ObservableCenterPoint.CeilingChangedEventArgs"/> instance containing the event data.</param>
        void BuyCeilingChanged(ObservableCenterPoint sender, ObservableCenterPoint.CeilingChangedEventArgs e);

        /// <summary>
        /// The ceiling changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ObservableCenterPoint.CeilingChangedEventArgs"/> instance containing the event data.</param>
        void SellCeilingChanged(ObservableCenterPoint sender, ObservableCenterPoint.CeilingChangedEventArgs e);

        void OrderStatusChanged(ObservableOrder sender, ObservableOrder.StatusUpdateEventArgs e);

        void TrendUpdate(TracableValue newTrend);

        /// <summary>
        /// Shuts down this instance.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Gets or sets the executing command.
        /// </summary>
        /// <value>
        /// The executing command.
        /// </value>
        TradingBot.Command ExecutingBuyCommand { get; set; }

        /// <summary>
        /// Gets or sets the executing sell command.
        /// </summary>
        /// <value>
        /// The executing sell command.
        /// </value>
        TradingBot.Command ExecutingSellCommand { get; set; }

        /// <summary>
        /// Maybe update.
        /// </summary>
        /// <returns></returns>
        Task MaybeUpdate();

        /// <summary>
        /// Gets or sets the now.
        /// </summary>
        /// <value>
        /// The now.
        /// </value>
        DateTime Now { get; set; }

        /// <summary>
        /// Gets or sets the sell point.
        /// </summary>
        /// <value>
        /// The sell point.
        /// </value>
        CeilingData SellPoint { get; set; }

        /// <summary>
        /// Gets or sets the buy point.
        /// </summary>
        /// <value>
        /// The buy point.
        /// </value>
        CeilingData BuyPoint { get; set; }
    }
}
