using System.Collections.Generic;

namespace Bot.NamebaseClient.Responses
{
    public class SymbolInformation
    {
        public string Symbol { get; set; }
        public string Status { get; set; }
        public string BaseAsset { get; set; }
        public int BasePrecision { get; set; }
        public string QuoteAsset { get; set; }
        public int QuotePrecision { get; set; }
        public List<OrderType> OrderTypes { get; set; }
    }
}