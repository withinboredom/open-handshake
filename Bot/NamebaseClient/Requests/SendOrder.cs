using Bot.NamebaseClient.Responses;

namespace Bot.NamebaseClient.Requests
{
    public class SendOrder
    {
        public string Symbol { get; set; }
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public string Quantity { get; set; }
        public string Price { get; set; }
        public long Timestamp { get; set; }
    }
}