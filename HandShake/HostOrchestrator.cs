using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace HandShake
{
    internal class HostOrchestrationInput
    {
        public HostMessage message { get; set; }

        public int Try { get; set; }

        public bool WasAlive { get; set; }
    }

    public static class HostOrchestrator
    {
        public static string calculateHash(HostMessage message)
        {
            using var hasher = SHA256.Create();
            var bytes = hasher.ComputeHash(Encoding.UTF8.GetBytes($"{message.Host}"));
            var builder = new StringBuilder();

            foreach (var t in bytes) builder.Append(t.ToString("x2"));

            return builder.ToString();
        }

        [FunctionName(nameof(HostOrchestrator))]
        public static async Task<bool> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            var input = context.GetInput<HostOrchestrationInput>();
            var me = context.InstanceId;

            var targetId = new EntityId(nameof(HostEntity), calculateHash(input.message));

            if (input.Try > 5) return false;

            var client = context.CreateEntityProxy<IHostEntity>(targetId);

            if(!await client.TryGetOrchestrationLock(context.InstanceId))
            {
                logger.LogError("Unable to get lock for {ipAddress}", input.message.Host);
                return false;
            }

            var isThere = await client.CheckUp(input.message);
            var jitter = (new Random()).Next(-5, 5);
#if DEBUG
            var minutes = 5;
#else
            var minutes = 60 + jitter;
#endif

            var nextTime = context.CurrentUtcDateTime.AddMinutes(minutes);
            var timer = context.CreateTimer(nextTime, CancellationToken.None);

            if (!isThere)
            {
                if (input.WasAlive)
                {
                    context.SignalEntity(HostsCounter.Id, HostsCounter.HostDead);
                    context.SignalEntity(HostList.Id, HostList.RemoveHost, targetId);
                }

                input.Try += 1;
                await timer;
                context.ContinueAsNew(input);
                return false;
            }

            input.Try = 0;
            input.WasAlive = true;
            context.SignalEntity(HostsCounter.Id, HostsCounter.HostAlive);
            context.SignalEntity(HostList.Id, HostList.AddHost, targetId);
            await timer;
            context.ContinueAsNew(input);

            return true;
        }
    }
}