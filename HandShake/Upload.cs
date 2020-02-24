using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HandShake
{
    public class HostMessage
    {
        public string Host { get; set; }
        public long Port { get; set; }
    }

    public static class Upload
    {
        [FunctionName("Upload")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "upload")]
            HttpRequest req,
            [Queue("hosts")] IAsyncCollector<HostMessage> messages,
            ILogger log)

        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            foreach (var addressData in data.addrs)
            {
                string hostString = addressData.addr.ToString();
                try
                {
                    var isFour = true;
                    var parts = hostString.Split('@');
                    if (parts[0].StartsWith('['))
                    {
                        isFour = false;
                        parts = parts.Length == 1 ? parts[0].Split(']') : parts[1].Split(']');
                        parts[0] = parts[0].Trim('[', ']');
                        parts[1] = parts[1].Trim(':');
                    }
                    else
                    {
                        parts = parts.Length == 1 ? parts[0].Split(':') : parts[1].Split(':');

                        if (parts[0].Split('.').Length != 4) throw new Exception();
                    }

                    await messages.AddAsync(new HostMessage
                    {
                        Port = long.Parse(parts[1]),
                        Host = parts[0],
                    });
                }
                catch (Exception)
                {
                    log.LogInformation($"Unable to parse: {hostString}");
                }
            }

            return new OkObjectResult("all done!");
        }
    }
}