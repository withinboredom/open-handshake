using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace HandShake
{
    public static class QueueHandler
    {
        [FunctionName(nameof(QueueHandler))]
        public static void Run([QueueTrigger("hosts")]HostMessage myQueueItem, [DurableClient] IDurableOrchestrationClient client, ILogger log)
        {
            var input = new HostOrchestrationInput {message = myQueueItem, Try = 0};
            log.LogInformation("Beginning host orchestrator for {ipAddress}", myQueueItem.Host);
            client.StartNewAsync(nameof(HostOrchestrator), input);
        }
    }
}
