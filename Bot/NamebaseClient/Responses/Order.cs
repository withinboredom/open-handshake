namespace Bot.NamebaseClient.Responses
{
    public class Order
    {
        public long OrderId { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalQuantity { get; set; }
        public decimal ExecutedQuantity { get; set; }
        public OrderStatus Status { get; set; }
        public OrderType Type { get; set; }
        public OrderSide Side { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
    }
}