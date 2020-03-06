using System;
using System.Threading.Tasks;
using Bot.NamebaseClient.Requests;
using Bot.NamebaseClient.Responses;
using Microsoft.Extensions.Logging;

namespace Bot.NamebaseClient
{
    public class ObservableOrder
    {
        public delegate void OrderStatusUpdateHandler(object sender, StatusUpdateEventArgs e);

        private readonly Client _client;
        private readonly ILogger _logger;

        public bool IsDeleted { get; private set; }

        public ObservableOrder(Order order, Client client, ILogger logger)
        {
            RawOrder = order;
            _client = client;
            _logger = logger;
            IsDeleted = false;
            ChartOrder();
        }

        private void ChartOrder()
        {
            if (IsDeleted) return;
            ChartGraphics.OrderPositions[RawOrder.OrderId] = new ChartGraphics.OrderPosition
            {
                Level = RawOrder.Price,
                StartTime = RawOrder.CreatedAt.ToDateTime().ToLocalTime(),
                EndTime = DateTime.Now
            };
        }

        public decimal Quantity => RawOrder.OriginalQuantity;
        public decimal Price => RawOrder.Price;

        public Order RawOrder { get; private set; }

        public event OrderStatusUpdateHandler? StatusChanged;

        public async Task Update()
        {
            var previous = RawOrder;
            RawOrder = await _client.GetOrder(RawOrder.OrderId);

            var handler = StatusChanged;
            if (previous.Status != RawOrder.Status)
            {
                if (RawOrder.Status == OrderStatus.PARTIALLY_FILLED)
                {
                    ChartGraphics.OrderFills.Add(new ChartGraphics.OrderFill
                    {
                        Level = RawOrder.Price,
                        Side = RawOrder.Side,
                        Time = DateTime.Now,
                        IsFilled = false,
                    });
                }
                else if (RawOrder.Status == OrderStatus.FILLED)
                {
                    ChartGraphics.OrderFills.Add(new ChartGraphics.OrderFill
                    {
                        Level = RawOrder.Price,
                        Side = RawOrder.Side,
                        Time = DateTime.Now,
                        IsFilled = true,
                    });
                }

                handler?.Invoke(this, new StatusUpdateEventArgs
                {
                    NewStatus = RawOrder.Status,
                    PreviousStatus = previous.Status
                });
            }

            ChartOrder();
        }

        public async Task Update(decimal quantity, decimal price, OrderType type = OrderType.LMT)
        {
            var previous = RawOrder;
            _logger.LogInformation($"Cancelling order {RawOrder.OrderId}");
            await _client.CancelOrder(RawOrder.OrderId);

            if (quantity > 0)
            {
                try
                {
                    RawOrder = await _client.CreateOrder(
                        type == OrderType.LMT
                            ? new SendOrder
                            {
                                Side = RawOrder.Side,
                                Price = price.ToString(),
                                Quantity = quantity.ToString(),
                                Type = RawOrder.Type
                            }
                            : new SendOrder
                            {
                                Side = RawOrder.Side,
                                Quantity = quantity.ToString(),
                                Type = OrderType.MKT
                            });
                    ChartOrder();
                    _logger.LogInformation($"Created {RawOrder.OrderId}");
                    IsDeleted = false;
                }
                catch (ErrorResponse.OutOfMoney)
                {
                    IsDeleted = true;
                }
            }
            else
            {
                IsDeleted = true;
                _logger.LogInformation("Deleted order");
            }

            /*var handler = StatusChanged;
            handler?.Invoke(this, new StatusUpdateEventArgs
            {
                NewStatus = _order.Status,
                PreviousStatus = previous.Status
            });*/
        }

        public class StatusUpdateEventArgs : EventArgs
        {
            public OrderStatus PreviousStatus { get; set; }
            public OrderStatus NewStatus { get; set; }
        }
    }
}