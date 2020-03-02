using System.Collections.Generic;

namespace Bot.NamebaseClient.Responses
{
    public class InformationResponse
    {
        public string Timezone { get; set; }
        public long ServerTime { get; set; }
        public List<SymbolInformation> Symbols { get; set; }
    }
}