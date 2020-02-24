using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace HandShake
{
    public static class HostsCounter
    {
        public const string HostAlive = "hostalive";
        public const string HostDead = "hostdead";

        public static EntityId Id => new EntityId(nameof(HostsCounter), nameof(HostsCounter));

        [FunctionName(nameof(HostsCounter))]
        public static void Counter([EntityTrigger] IDurableEntityContext context)
        {
            var currentValue = context.GetState<int>();
            switch(context.OperationName)
            {
                case HostAlive:
                    currentValue += 1;
                    break;
                case HostDead:
                    currentValue -= 1;
                    break;
            }
            context.SetState(currentValue);
        }
    }
}
