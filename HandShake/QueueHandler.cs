using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace HandShake
{
    public static class QueueHandler
    {
        /*[FunctionName(nameof(QueueHandler))]
        public static async Task Run([QueueTrigger("hosts")] HostMessage myQueueItem,
            [DurableClient] IDurableEntityClient client, ILogger log)
        {
            await client.SignalEntityAsync<IHostEntity>(HostEntity.Id(myQueueItem.Host),
                entity => entity.SetIp(myQueueItem.Host));
            await client.SignalEntityAsync<IHostEntity>(HostEntity.Id(myQueueItem.Host),
                entity => entity.CheckUp());
        }*/
    }
}