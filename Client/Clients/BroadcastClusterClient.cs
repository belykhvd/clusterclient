using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using log4net;

namespace Client.Clients
{
    public class BroadcastClusterClient : ClusterClientBase
    {
        public BroadcastClusterClient(IEnumerable<string> replicaAddresses) : base(replicaAddresses)
        {
        }

        public override async Task<string> ProceedRequestAsync(string query, TimeSpan timeout)
        {
            var mappingTaskToUriGuidTuple = new ConcurrentDictionary<Task<string>, (string, Guid)>(
                ClusterManager.EnumerateWhiteReplica().Select(uri =>
                {
                    var guid = Guid.NewGuid();
                    var task = Task.Run(async () =>
                    {
                        var requestTask = WebRequestTask(uri, query, guid);
                        await Task.WhenAny(requestTask, Task.Delay(timeout));

                        if (requestTask.IsCompleted && !requestTask.IsFaulted)
                            return requestTask.Result;

                        ClusterManager.MarkGrey(uri, query, TimeSpan.FromMilliseconds(500));
                        throw new TimeoutException();
                    });

                    return new KeyValuePair<Task<string>, (string, Guid)>(task, (uri, guid));
                }));

            var parallelTasks = new HashSet<Task<string>>(mappingTaskToUriGuidTuple.Keys);
            while (parallelTasks.Any())
            {
                var completedTask = await Task.WhenAny(parallelTasks);
                if (completedTask.IsFaulted)
                {                    
                    parallelTasks.Remove(completedTask);                    
                    Log.Info($"Task [{query}] faulted with [{completedTask.Exception?.GetBaseException().Message}].");
                }
                else
                {
                    foreach (var task in parallelTasks.Where(t => !t.IsCompleted))
                    {                        
                        var (uri, guid) = mappingTaskToUriGuidTuple[task];
                        ProceedCancelRequest(uri, guid);
                    }

                    return completedTask.Result;                    
                }
            }

            throw new TimeoutException();
        }

        protected override ILog Log => LogManager.GetLogger(typeof(BroadcastClusterClient));
    }
}