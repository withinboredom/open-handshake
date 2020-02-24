using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace HandShake
{
    public static class Locker
    {
        [FunctionName(nameof(Locker))]
        public static async Task Run([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var releaseId = context.GetInput<EntityId>();
            var releaseTime = context.CurrentUtcDateTime.AddMinutes(65);

            var cancellationSource = new CancellationTokenSource();

            var timer = context.CreateTimer(releaseTime, cancellationSource.Token);
            var signal = context.WaitForExternalEvent("lock");

            var result = await Task.WhenAny(timer, signal);

            if (result == timer)
            {
                await context.CallEntityAsync(releaseId, "ReleaseLock");
            } else
            {
                // renew the lock
                cancellationSource.Cancel();
                context.ContinueAsNew(releaseId);
            }
        }
    }
}
