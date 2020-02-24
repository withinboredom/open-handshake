using System;
using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace HandShake
{
    public static class Cleaner
    {
        [FunctionName("Cleaner")]
        public static void Run([TimerTrigger("0 0 */5 * * *")]TimerInfo myTimer, [DurableClient] IDurableOrchestrationClient client, ILogger log)
        {
            client.PurgeInstanceHistoryAsync(DateTime.Now - TimeSpan.FromHours(24), null,
                new[]
                {
                    OrchestrationStatus.Canceled, OrchestrationStatus.Completed, OrchestrationStatus.Failed,
                    OrchestrationStatus.Terminated
                });

            log.LogWarning("Purged history");
        }
    }
}
