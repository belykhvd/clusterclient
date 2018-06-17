using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using log4net;

namespace Client.Clients
{
    public class SmartClusterClient : ClusterClientBase
    {
        public SmartClusterClient(IEnumerable<string> replicaAddresses) : base(replicaAddresses)
        {
        }

        public override async Task<string> ProceedRequestAsync(string query, TimeSpan timeout)
        {
            var allRequestsTask = InnerProceedRequestAsync(query, timeout);
            await Task.WhenAny(allRequestsTask, Task.Delay(timeout));

            if (allRequestsTask.IsCompleted && !allRequestsTask.IsFaulted)
                return allRequestsTask.Result;

            throw new TimeoutException();
        }

        private async Task<string> InnerProceedRequestAsync(string query, TimeSpan timeout)
        {
            var shuffledAddresses = new Queue<string>(ClusterManager.ShuffledPrioritizedWhitePeplicas());
            var specificTimeout = timeout.TotalMilliseconds / ReplicaAddresses.Length;

            var lastReplica = shuffledAddresses.Dequeue();
            var guid = Guid.NewGuid();
            var firstTask = WebRequestTask(lastReplica, query, guid);
            var parallelTasks = new HashSet<Task> {firstTask};

            var taskReplicaMapping = new Dictionary<Task, string> {{firstTask, lastReplica}};

            var delayTask = Task.Delay((int)specificTimeout);
            parallelTasks.Add(delayTask);
            
            while (parallelTasks.Any())
            {                                               
                var completedTask = await Task.WhenAny(parallelTasks);                
                if (completedTask == delayTask)
                {
                    ClusterManager.MarkGrey(lastReplica, query, TimeSpan.FromMilliseconds(500));

                    if (shuffledAddresses.Any())
                    {
                        lastReplica = shuffledAddresses.Dequeue();

                        guid = Guid.NewGuid();
                        var nextReplicaTask = WebRequestTask(lastReplica, query, guid);
                        taskReplicaMapping[nextReplicaTask] = lastReplica;
                        delayTask = Task.Delay((int)specificTimeout);
                        
                        parallelTasks.Add(nextReplicaTask);
                        parallelTasks.Add(delayTask);
                    }                    
                }
                else
                {
                    if (!completedTask.IsFaulted)                        
                        return ((Task<string>) completedTask).Result;

                    ClusterManager.MarkGrey(taskReplicaMapping[completedTask], query, TimeSpan.FromMilliseconds(500));
                }

                parallelTasks.Remove(completedTask);
            }            

            throw new TimeoutException();
        }

        protected override ILog Log => LogManager.GetLogger(typeof(SmartClusterClient));
    }
}