using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Bot.NamebaseClient.Requests;
using Bot.NamebaseClient.Responses;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bot.NamebaseClient
{
    public class Client
    {
        public enum Heavy
        {
            Top,
            Bottom,
            None
        }

        private const string Api = "https://www.namebase.io/api/v0";
        private const string Symbol = "HNSBTC";
        private readonly HttpClient _client;
        private readonly ILogger _logger;
        private TimeSpan _clockDrift;

        public int NumberRetries = 3;

        public Client(Program.Auth auth, HttpClient client, ILogger logger)
        {
            _client = client;
            _logger = logger;

            var key = $"{auth.Key}:{auth.Secret}";
            key = Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", key);
            _clockDrift = TimeSpan.Zero;
            logger.LogTrace("Client initialized");
        }

        private Task<HttpResponseMessage> GetWithRetry(string path)
        {
            _logger.LogDebug($"GET API: {path}");
            return DoRetry(() => _client.GetAsync(path), NumberRetries);
        }

        private Task<HttpResponseMessage> SendWithRetry(HttpRequestMessage message)
        {
            _logger.LogDebug($"DELETE API");
            return DoRetry(() => _client.SendAsync(message), NumberRetries);
        }

        private Task<HttpResponseMessage> PostWithRetry(string requestUri, HttpContent content)
        {
            _logger.LogDebug($"POST API: {requestUri}");
            return DoRetry(() => _client.PostAsync(requestUri, content), NumberRetries);
        }

        private async Task<HttpResponseMessage> DoRetry(Func<Task<HttpResponseMessage>> retry, int remaining)
        {
            while (remaining > 0)
            {
                var response = await retry();
                if (response.IsSuccessStatusCode) return response;

                var error = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                switch (error.HowRecovery())
                {
                    case ErrorResponse.SuggestedRecovery.SyncTime:
                        await GetInfo();
                        await Task.Delay(TimeSpan.FromMilliseconds(500));
                        _logger.LogCritical("Updating clock drift!");
                        throw new ErrorResponse.TimeChanged();
                    case ErrorResponse.SuggestedRecovery.Fatal:
                        _logger.LogCritical("Fatal error!");
                        throw new ErrorResponse.FatalError();
                    case ErrorResponse.SuggestedRecovery.Ignore:
                        _logger.LogCritical("Ignoring error!");
                        throw new ErrorResponse.IgnoreFailure();
                    case ErrorResponse.SuggestedRecovery.Retry:
                    default:
                        _logger.LogCritical("Retrying!");
                        var delay = remaining switch
                        {
                            1 => TimeSpan.FromSeconds(10),
                            2 => TimeSpan.FromSeconds(1),
                            3 => TimeSpan.FromMilliseconds(500),
                            _ => TimeSpan.FromSeconds(3)
                        };
                        await Task.Delay(delay);
                        remaining -= 1;
                        break;
                }
                _logger.LogDebug($"Retrying {remaining}");
            }

            throw new ErrorResponse.FatalError();
        }

        public async Task<InformationResponse> GetInfo()
        {
            var response = await GetWithRetry($"{Api}/info");
            var info = JsonConvert.DeserializeObject<InformationResponse>(await response.Content.ReadAsStringAsync());
            _clockDrift = TimeSpan.FromMilliseconds(DateTime.UtcNow.ToUnixTime() - info.ServerTime);
            return info;
        }

        public async Task<(CeilingData, CeilingData)> GetCenterPoint()
        {
            try
            {
                var response = await GetWithRetry($"{Api}/depth?symbol={Symbol}");
                var depth = JsonConvert.DeserializeObject<DepthResponse>(await response.Content.ReadAsStringAsync());
                //await JsonSerializer.DeserializeAsync<DepthResponse>(await response.Content.ReadAsStreamAsync());

                var lowest = new CeilingData(depth.Bids, true);
                var highest = new CeilingData(depth.Asks, false);

                return (lowest, highest);
            }
            catch (ErrorResponse.TimeChanged)
            {
                _logger.LogWarning($"Detected clock drift!");
                return await GetCenterPoint();
            }
        }

        public async Task<IEnumerable<Order>> GetExistingOrders(long? minOrderId = null,
            Func<Order, bool>? filter = null)
        {
            try
            {
                var orderString = minOrderId == null ? string.Empty : $"&orderId={minOrderId}";
                var response =
                    await GetWithRetry(
                        $"{Api}/order/all?symbol={Symbol}&timestamp={(DateTime.UtcNow + _clockDrift).ToUnixTime()}&receiveWindow=3000&limit=1000{orderString}");
                var data = JsonConvert.DeserializeObject<List<Order>>(await response.Content.ReadAsStringAsync());

                return filter != null
                    ? data.Where(order => order.Price < 0.00007500m).Where(filter)
                    : data.Where(order => order.Price < 0.00007500m);
            }
            catch (ErrorResponse.TimeChanged)
            {
                _logger.LogWarning("Detected clock drift");
                return await GetExistingOrders(minOrderId, filter);
            }
        }

        public async Task CancelOrder(long orderId)
        {
            try
            {
                var message = new HttpRequestMessage(HttpMethod.Delete, $"{Api}/order")
                {
                    Content = new StringContent(new
                    {
                        Symbol,
                        orderId,
                        Timestamp = (DateTime.UtcNow + _clockDrift).ToUnixTime()
                    }.ToJson(), Encoding.UTF8, "application/json")
                };
                var response = await SendWithRetry(message);
                var data = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
            }
            catch (ErrorResponse.TimeChanged)
            {
                _logger.LogWarning($"Detected clock drift");
                await CancelOrder(orderId);
            }
            catch (ErrorResponse.IgnoreFailure)
            {
                _logger.LogWarning("Attempted to cancel order that cannot be canceled");
            }
        }

        public static decimal ConvertBtcToHns(decimal btc, decimal conversionRate)
        {
            return 1m / conversionRate * btc;
        }

        public async Task CreateSpread(CeilingData ceiling, Heavy heavy, OrderSide side, int numPositions = 5,
            decimal minDist = 0.00000005m)
        {
            var ordersToCancel = await GetExistingOrders(filter: order =>
                order.Side == side &&
                (order.Status == OrderStatus.PARTIALLY_FILLED || order.Status == OrderStatus.NEW));

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Canceling orders");
            var operations = new List<Task>();
            foreach (var orderToCancel in ordersToCancel) operations.Add(CancelOrder(orderToCancel.OrderId));

            await Task.WhenAll(operations);
            Console.ResetColor();

            // calculate positions
            var spread = Math.Abs(ceiling.Ceiling - ceiling.Bottom) / numPositions;

            if (spread == 0m) return;

            // calculate how much cash we have
            //await Task.Delay(TimeSpan.FromMilliseconds(500));

            var account = await GetAccount();
            var balance = side == OrderSide.SELL
                ? account.Balances.First(x => x.Asset == "HNS").Unlocked
                : account.Balances.First(x => x.Asset == "BTC").Unlocked;
            balance = 0.5m * balance; // never spend more than 75% of the available balance

            operations = new List<Task>();

            for (var i = 0; i < numPositions; i++)
            {
                var bid = 0m;
                switch (heavy)
                {
                    case Heavy.None:
                        bid = balance / numPositions;
                        break;
                    case Heavy.Bottom:
                        bid = (0.1m * i + 1m) / 55 * balance;
                        break;
                    case Heavy.Top:
                        bid = (0.1m * (numPositions - i) + 1m) / 55 * balance;
                        break;
                }

                var price = (ceiling.Ceiling - ceiling.Bottom) / numPositions;
                price = ceiling.Bottom + (side == OrderSide.SELL ? 1m : -1m) * minDist + price * i;

                if (Math.Abs(price - ceiling.Bottom) < minDist)
                    price = side == OrderSide.SELL ? price + minDist : price - minDist;

                if (side == OrderSide.BUY) bid = ConvertBtcToHns(bid, price);

                var order = new SendOrder
                {
                    Price = price.ToString(),
                    Side = side,
                    Quantity = bid.ToString(),
                    Timestamp = DateTime.UtcNow.ToUnixTime(),
                    Type = OrderType.LMT,
                    Symbol = Symbol
                };

                operations.Add(CreateOrder(order));
                Console.ForegroundColor = side == OrderSide.SELL ? ConsoleColor.Red : ConsoleColor.Green;
                Console.WriteLine($"{side.ToString()} order placed for {price} at {bid:F8}");
                Console.ResetColor();
            }

            await Task.WhenAll(operations);
        }

        public async Task<Account> GetAccount()
        {
            try
            {
                var response = await GetWithRetry($"{Api}/account?timestamp={(DateTime.UtcNow + _clockDrift).ToUnixTime()}");
                var acct = JsonConvert.DeserializeObject<Account>(await response.Content.ReadAsStringAsync());
                return acct;
            }
            catch (ErrorResponse.TimeChanged)
            {
                _logger.LogWarning("Detected clock drift");
                return await GetAccount();
            }
        }

        public async Task<Order> CreateOrder(SendOrder order)
        {
            try
            {
                order.Symbol = Symbol;
                order.Timestamp = (DateTime.UtcNow + _clockDrift).ToUnixTime();
                var response =
                    await PostWithRetry($"{Api}/order",
                        new StringContent(order.ToJson(), Encoding.UTF8, "application/json"));
                return JsonConvert.DeserializeObject<Order>(await response.Content.ReadAsStringAsync());
            }
            catch (ErrorResponse.TimeChanged)
            {
                _logger.LogWarning("Detected clock drift");
                return await CreateOrder(order);
            }
        }

        public async Task<Order> GetOrder(long orderId)
        {
            try
            {
                var response =
                    await GetWithRetry(
                        $"{Api}/order?symbol={Symbol}&orderId={orderId}&timestamp={(DateTime.UtcNow + _clockDrift).ToUnixTime()}");
                return JsonConvert.DeserializeObject<Order>(await response.Content.ReadAsStringAsync());
            }
            catch (ErrorResponse.TimeChanged)
            {
                _logger.LogWarning("Detected clock drift");
                return await GetOrder(orderId);
            }
        }
    }
}