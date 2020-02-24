using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HandShake
{
    public static class Stats
    {
        [FunctionName("Stats")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "stats")] HttpRequest req,
            [DurableClient] IDurableEntityClient client,
            ILogger log)
        {
            var counts = client.ReadEntityStateAsync<int>(HostsCounter.Id);

            return new OkObjectResult(new
            {
                hostsAlive = counts,
            });
        }

        private static void ExtractAll(List<object> output, IEnumerable<DurableEntityStatus> statuses, bool filter)
        {
            foreach(var entity in statuses)
            {
                try
                {
                    if(entity.State == null) continue;
                    var state = entity.State.ToObject<HostEntity>();
                    if (filter && state.DnsUptime > 0.9m)
                    {
                        AddTo(output, state);
                    }
                    else if (!filter)
                    {
                        AddTo(output, state);
                    }
                } catch { }
            }
        }

        private static void AddTo(List<object> output, HostEntity state)
        {
            output.Add(new
            {
                state.IpAddress,
                state.DnsUptime,
                state.ipv4Support,
                state.ipv6Support,
            });
        }

        [FunctionName(nameof(All))]
        public static async Task<IActionResult> All([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "all")] HttpRequest req, [DurableClient] IDurableEntityClient client, ILogger logger)
        {
            var token = string.Empty;
            var isAlive = req.Query["onlyAlive"] == "true";
            var getAll = req.Query["all"] == "true" || isAlive;

            var results = new List<object>();
            EntityQueryResult query;
            var examined = 0L;

            isAlive = true;

            if(isAlive)
            {
                if(client == null) logger.LogError("client is null!");
                var all = await client.ReadEntityStateAsync<List<EntityId>>(HostList.Id);
                if (all.EntityExists)
                {
                    var list = all.EntityState.Distinct();
                    foreach (var entity in list)
                    {
                        examined += 1;
                        var state = await client.ReadEntityStateAsync<HostEntity>(entity);
                        if (!state.EntityExists)
                        {
                            await client.SignalEntityAsync(HostList.Id, HostList.RemoveHost, entity);
                            continue;
                        }

                        AddTo(results, state.EntityState);
                    }
                }

                goto DONE;
            }

            do
            {
                query = await client.ListEntitiesAsync(new EntityQuery { FetchState = true, EntityName = nameof(HostEntity), ContinuationToken = token }, CancellationToken.None);
                token = query.ContinuationToken;
                examined += query.Entities.Count();
                ExtractAll(results, query.Entities, false);
            } while (!string.IsNullOrEmpty(query.ContinuationToken) || getAll);

            DONE:

            var response = new
            {
                examined,
                resultCount = results.Count,
                results
            };

            return new OkObjectResult(response);
        }
    }
}
