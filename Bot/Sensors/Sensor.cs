using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Bot.NamebaseClient;
using Bot.NamebaseClient.Responses;

namespace Bot.Sensors
{
    public class Sensor
    {
        private readonly Sources _source;
        private static Dictionary<Sources, Sensor> _sources;
        public TrackableValue Value { get; private set; }
        public bool HasChange { get; set; }

        public Sensor(Sources source)
        {
            _source = source;
            _sources = new Dictionary<Sources, Sensor>();
            Value = new TrackableValue();
        }

        public static Sensor Create(Sources source, ObservableAccount account, ObservableCenterPoint center, ObservableCollection<ObservableOrder> buyOrders)
        {
            if (_sources.ContainsKey(source)) return _sources[source];
            
            switch (source)
            {
                case Sources.AccountHnsTotal:
                    _sources[source] = new Sensor(source)
                    {
                        Value = new TrackableValue
                        {
                            LatestValue = account.Hns.Total,
                        }
                    };
                    account.HnsUpdated += (sender, args) =>
                    {
                        _sources[source].Value.LatestValue = args.NewAmount;
                    };
                    break;
                case Sources.AccountBtcTotal:
                    _sources[source] = new Sensor(source)
                    {
                        Value = new TrackableValue()
                        {
                            LatestValue = account.Btc.Total,
                        }
                    };
                    account.BtcUpdated += (sender, args) => { _sources[source].Value.LatestValue = args.NewAmount; };
                    break;
                case Sources.OrderBookBuys:
                    _sources[source] = new Sensor(source)
                    {
                        Value = new TrackableValue()
                        {
                            LatestValue = center.BuySide.Bottom,
                        }
                    };
                    center.BuyCeilingChanged += (sender, args) =>
                    {
                        _sources[source].Value.LatestValue = args.New.Bottom;
                    };
                    break;
                case Sources.OrderBookSells:
                    _sources[source] = new Sensor(source)
                    {
                        Value = new TrackableValue()
                        {
                            LatestValue = center.SellSide.Bottom,
                        }
                    };
                    center.SellCeilingChanged += (sender, args) =>
                    {
                        _sources[source].Value.LatestValue = args.New.Bottom;
                    };
                    break;
                case Sources.ResistanceBuys:
                    _sources[source] = new Sensor(source)
                    {
                        Value = new TrackableValue()
                        {
                            LatestValue = center.BuySide.Resistance[0].Level,
                        }
                    };
                    center.BuyCeilingChanged += (sender, args) =>
                    {
                        _sources[source].Value.LatestValue = args.New.Resistance[0].Level;
                    };
                    break;
                case Sources.ResistanceSells:
                    _sources[source] = new Sensor(source)
                    {
                        Value = new TrackableValue()
                        {
                            LatestValue = center.SellSide.Resistance[0].Level,
                        }
                    };
                    center.SellCeilingChanged += (sender, args) =>
                    {
                        _sources[source].Value.LatestValue = args.New.Resistance[0].Level;
                    };
                    break;
                case Sources.BuyOrderFilled:
                    _sources[source] = new Sensor(source);
                    foreach (var order in buyOrders)
                    {
                        order.StatusChanged += (sender, args) =>
                        {
                            if (args.NewStatus == OrderStatus.FILLED)
                            {
                                _sources[source].Value.LatestValue += 1;
                            }
                        };
                    }
                    buyOrders.CollectionChanged += (sender, args) =>
                    {
                        switch (args.Action)
                        {
                            case NotifyCollectionChangedAction.Add:
                                foreach (ObservableOrder? order in args.NewItems)
                                {
                                    if (order == null) continue;
                                    order.StatusChanged += OrderOnStatusChanged;
                                }
                                break;
                            case NotifyCollectionChangedAction.Remove:
                                break;
                            case NotifyCollectionChangedAction.Replace:
                                break;
                            case NotifyCollectionChangedAction.Move:
                                break;
                            case NotifyCollectionChangedAction.Reset:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    break;
                case Sources.BuyOrderPartial:
                    _sources[source] = new Sensor(source);
                    break;
                case Sources.BuyOrderCancelled:
                    break;
                case Sources.SellOrderFilled:
                    break;
                case Sources.SellOrderPartial:
                    break;
                case Sources.SellOrderCancelled:
                    break;
                case Sources.Time:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }

            return _sources[source];
        }

        private static void OrderOnStatusChanged(object sender, ObservableOrder.StatusUpdateEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}