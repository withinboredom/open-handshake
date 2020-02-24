using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace HandShake
{
    public static class HostList
    {
        public const string AddHost = "addhost";
        public const string RemoveHost = "removehost";
        public static EntityId Id => new EntityId(nameof(HostList), nameof(HostList));

        [FunctionName(nameof(HostList))]
        public static void Aggregate([EntityTrigger] IDurableEntityContext context)
        {
            var state = context.GetState(() => new List<EntityId>());
            var input = context.GetInput<EntityId>();

            switch (context.OperationName)
            {
                case AddHost:
                    state.Add(input);
                    break;
                case RemoveHost:
                    state.RemoveAll(x => x.Equals(input));
                    break;
            }

            state = state.Distinct().ToList();

            context.SetState(state);
        }
    }
}
