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
                handler?.Invoke(this, new StatusUpdateEventArgs
                {
                    NewStatus = RawOrder.Status,
                    PreviousStatus = previous.Status
                });
        }

        public async Task Update(decimal quantity, decimal price)
        {
            var previous = RawOrder;
            _logger.LogInformation($"Cancelling order {RawOrder.OrderId}");
            await _client.CancelOrder(RawOrder.OrderId);

            if (quantity > 0)
            {
                RawOrder = await _client.CreateOrder(
                    new SendOrder
                    {
                        Side = RawOrder.Side,
                        Price = price.ToString(),
                        Quantity = quantity.ToString(),
                        Type = RawOrder.Type
                    });
                _logger.LogInformation($"Created {RawOrder.OrderId}");
                IsDeleted = false;
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