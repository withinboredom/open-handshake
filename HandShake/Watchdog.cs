using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace HandShake
{
    /// <summary>
    ///     Watchdog for host entities
    /// </summary>
    public static class Watchdog
    {
        /// <summary>
        ///     Gets the state of the host.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="client">The client.</param>
        /// <returns></returns>
        [FunctionName(nameof(GetHostState))]
        public static Task<EntityStateResponse<HostEntity>> GetHostState(
            [ActivityTrigger] IDurableActivityContext context, [DurableClient] IDurableEntityClient client)
        {
            var hostId = context.GetInput<EntityId>();
            return client.ReadEntityStateAsync<HostEntity>(hostId);
        }

        /// <summary>
        ///     Runs the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="logger">The logger.</param>
        /// <returns></returns>
        [FunctionName(nameof(Watchdog))]
        public static async Task<bool> Run([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            var hostId = context.GetInput<EntityId>();
            logger = context.CreateReplaySafeLogger(logger);

            var state = await context.CallActivityAsync<EntityStateResponse<HostEntity>>(nameof(GetHostState), hostId);

            if (!state.EntityExists)
            {
                logger.LogError("Attempted to start watch dog for a non-existing host entity: {hostId}", hostId);
                throw new Exception("Failed to start watch dog for non-existing entity");
            }

            if (context.InstanceId != HostEntity.calculateHash(state.EntityState.IpAddress))
                throw new Exception("violent suicide committed on watchdog!");

            if (state.EntityState.ipv4Support || state.EntityState.ipv6Support)
            {
                context.SignalEntity(HostList.Id, HostList.AddHost, hostId);
                logger.LogInformation("Adding {hostId} to active hosts", hostId);
            }
            else
            {
                context.SignalEntity(HostList.Id, HostList.RemoveHost, hostId);
                logger.LogInformation("Removing {hostId} from active hosts", hostId);
            }

            var nextCheck = state.EntityState.DnsUptime switch
            {
                { } v when v < 0.2m => TimeSpan.FromDays(1),
                { } v when v >= 0.2m && v < 0.75m => TimeSpan.FromHours(12 + new Random().Next(-1, 1)),
                { } v when v >= 0.75m => TimeSpan.FromMinutes(60 + new Random().Next(-5, 5)),
                _ => TimeSpan.FromHours(1)
            };

#if DEBUG
            nextCheck = TimeSpan.FromMinutes(5);
#endif

            await context.CreateTimer(context.CurrentUtcDateTime + nextCheck, CancellationToken.None);

            var host = context.CreateEntityProxy<IHostEntity>(hostId);

            await host.CheckUp();

            context.ContinueAsNew(hostId);

            return true;
        }
    }
}