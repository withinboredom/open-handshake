using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace HandShake
{
    public static class WatchdogMigration
    {
        [FunctionName(nameof(DoCheckUp))]
        public static Task DoCheckUp([QueueTrigger("migration")] EntityId hostId,
            [DurableClient] IDurableEntityClient client, ILogger logger)
        {
            logger.LogInformation("Signaling {hostId}", hostId);
            return client.SignalEntityAsync<IHostEntity>(hostId, entity => entity.CheckUp());
        }

        /*
        [FunctionName(nameof(HandleList))]
        public static async Task HandleList([QueueTrigger("list-migration")] NextQueue next, [DurableClient] IDurableEntityClient client, [Queue("migration")] IAsyncCollector<EntityId> toMigrate, [Queue("list-migration")] IAsyncCollector<NextQueue> nextList, ILogger logger)
        {
            logger.LogInformation("Starting migration on set {nextId}", next);

            var nextToken = await DoQuery(next.token, toMigrate, client);

            if (!string.IsNullOrEmpty(nextToken))
            {
                await nextList.AddAsync(new NextQueue {token = nextToken});
            }
        }*/

        public class NextQueue
        {
            public string token { get; set; }
        }

        private static async Task<string> DoQuery(string next, IAsyncCollector<EntityId> toMigrate, IDurableEntityClient client)
        {
            var query = new EntityQuery
            {
                ContinuationToken = next,
                EntityName = nameof(HostEntity),
                FetchState = false,
            };

            var result = await client.ListEntitiesAsync(query, CancellationToken.None);

            foreach (var entity in result.Entities)
            {
                await toMigrate.AddAsync(entity.EntityId);
            }

            return result.ContinuationToken;
        }

        [FunctionName(nameof(TriggerMigration))]
        [return: Queue("list-migration")]
        public static async Task<NextQueue> TriggerMigration([HttpTrigger(AuthorizationLevel.Admin, "get", Route = "migrate")] HttpRequest req, ILogger logger, [Queue("migration")] IAsyncCollector<EntityId> toMigrate, [DurableClient] IDurableEntityClient client)
        {
            logger.LogInformation("Triggering migration");
            return new NextQueue {token = await DoQuery(null, toMigrate, client)};
        }

        [FunctionName(nameof(BrokenCheck))]
        public static async Task<ActionResult> BrokenCheck([HttpTrigger(AuthorizationLevel.Admin, "get", Route = "broke")]
            HttpRequest req, ILogger logger, [DurableClient] IDurableEntityClient client, [Queue("migration")] IAsyncCollector<EntityId> queue)
        {
            string token = null;

            var ids = new List<EntityId>();

            do
            {
                var query = new EntityQuery
                {
                    ContinuationToken = token,
                    EntityName = nameof(HostEntity),
                    FetchState = false,
                    PageSize = 5000,
                };
                var result = await client.ListEntitiesAsync(query, CancellationToken.None);
                token = result.ContinuationToken;


                foreach (var entity in result.Entities)
                {
                    if(ids.Contains(entity.EntityId)) continue;
                    ids.Add(entity.EntityId);
                }

                break;
            } while (!string.IsNullOrEmpty(token));

            foreach (var id in ids)
            {
                await queue.AddAsync(id);
            }

            return new OkObjectResult(new { items = ids.Count });
        }
    }
}
