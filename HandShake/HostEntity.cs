using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DnsClient;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HandShake
{
    public interface IHostEntity
    {
        Task<bool> CheckUp();
        Task SetIp(string ipAddress);
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class HostEntity : IHostEntity
    {
        public static HttpClient _client = new HttpClient();

        private ILogger _logger;

        public HostEntity(ILogger logger)
        {
            _logger = logger;
        }

        [JsonProperty] public decimal Uptime { get; set; }

        [JsonProperty] public decimal DnsUptime { get; set; }

        [JsonProperty] public int NumberSamples { get; set; }

        [JsonProperty] public string IpAddress { get; set; }

        [JsonProperty] public bool ipv4Support { get; set; }

        [JsonProperty] public bool HandshakeSupport { get; set; }

        [JsonProperty] public DateTime? MonitoringSince { get; set; }

        [JsonProperty] public DateTime? LastChecked { get; set; }

        public Task SetIp(string ipAddress)
        {
            IpAddress = ipAddress;
            return Task.CompletedTask;
        }

        public async Task<bool> CheckUp()
        {
            if(string.IsNullOrWhiteSpace(IpAddress))
            {
                _logger.LogCritical("Host with no address: {hostId}", Entity.Current.EntityId);
                Entity.Current.DeleteState();
                return false;
            }

            Entity.Current.StartNewOrchestration(nameof(Watchdog), Entity.Current.EntityId, calculateHash(IpAddress));

            if (MonitoringSince == null) MonitoringSince = DateTime.UtcNow;

            NumberSamples += 1;

            LastChecked = DateTime.UtcNow;

            var endpoint = new IPEndPoint(IPAddress.Parse(IpAddress), 53);
            var client = new LookupClient(endpoint)
                {UseTcpFallback = true, Timeout = TimeSpan.FromSeconds(2), ThrowDnsErrors = false};

            var hasDns = false;
            try
            {
                var dns = await client.QueryAsync("google.com", QueryType.A);
                hasDns = !dns.HasError;
            }
            catch
            {
                hasDns = false;
            }

            if (!hasDns)
            {
                ipv4Support = false;
            }
            else
            {
                ipv4Support = true;
                _logger.LogMetric("ipv4", 1);
            }

            try
            {
                var handshake = await client.QueryAsync("batch", QueryType.TXT);
                HandshakeSupport = handshake.Answers.Any();
            }
            catch
            {
                HandshakeSupport = false;
            }

            UpdateDnsResult(hasDns && HandshakeSupport);

            return hasDns;
        }

        public static string calculateHash(string message)
        {
            using var hasher = SHA256.Create();
            var bytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(message));
            var builder = new StringBuilder();

            foreach (var t in bytes) builder.Append(t.ToString("x2"));

            return builder.ToString();
        }

        public static EntityId Id(string ipAddress)
        {
            return new EntityId(nameof(HostEntity), calculateHash(ipAddress));
        }

        private void UpdateUptime(bool isSuccess)
        {
            var alpha = 2 / (NumberSamples + 1);
            Uptime = alpha * (isSuccess ? 1 : 0) + (1 - alpha) * Uptime;
        }

        private void UpdateDnsResult(bool isSuccess)
        {
            var alpha = 2m / (decimal) (NumberSamples + 1m);
            DnsUptime = alpha * (isSuccess ? 1m : 0m) + (1 - alpha) * DnsUptime;
        }

        [FunctionName(nameof(HostEntity))]
        public static Task Run([EntityTrigger] IDurableEntityContext context, ILogger logger)
        {
            return context.DispatchAsync<HostEntity>(logger);
        }
    }
}