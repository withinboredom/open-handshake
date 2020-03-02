using System.Collections.Generic;

namespace Bot.NamebaseClient.Responses
{
    public class DepthResponse
    {
        public long LastEventId { get; set; }
        public List<List<string>> Bids { get; set; }
        public List<List<string>> Asks { get; set; }
    }
}