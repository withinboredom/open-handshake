using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HandShake
{
    public static class Crud
    {
        [FunctionName(nameof(Create))]
        public static async Task<IActionResult> Create(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "host/{ip}")]
            HttpRequest req,
            string ip,
            [Queue("hosts")] IAsyncCollector<HostMessage> messages,
            ILogger log)
        {
            try
            {
                IPAddress.Parse(ip);
            }
            catch
            {
                return new BadRequestObjectResult(new {error = "Invalid ip address"});
            }

            log.LogInformation("Adding {ipAddress} to bucket", ip);
            await messages.AddAsync(new HostMessage {
                Host = ip,
                Port = 53,
            });

            return new OkResult();
        }

        [FunctionName(nameof(Read))]
        public static async Task<IActionResult> Read(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "host/{ip}")]
            HttpRequest req, string ip, [DurableClient] IDurableEntityClient client, ILogger logger)
        {
            try
            {
                IPAddress.Parse(ip);
            } catch
            {
                return new BadRequestResult();
            }

            var entity = await client.ReadEntityStateAsync<HostEntity>(new EntityId(nameof(HostEntity),
                HostOrchestrator.calculateHash(new HostMessage {Host = ip})));

            if (!entity.EntityExists) return new NotFoundResult();

            return new OkObjectResult(new
            {
                uptime = entity.EntityState.DnsUptime * 100m + "%", ipAddress = entity.EntityState.IpAddress,
                monitoringSince = entity.EntityState.MonitoringSince, ARecords = entity.EntityState.ipv4Support,
                isRecursive = true, isHandshake = (bool?) null,
            });
        }
    }
}