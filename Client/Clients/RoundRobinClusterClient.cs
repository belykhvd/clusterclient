using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using log4net;

namespace Client.Clients
{
    public class RoundRobinClusterClient : ClusterClientBase
    {
        public RoundRobinClusterClient(IEnumerable<string> replicaAddresses) : base(replicaAddresses)
        {
        }

        public override async Task<string> ProceedRequestAsync(string query, TimeSpan timeout)
        {
            var shuffledAddresses = ClusterManager.ShuffledPrioritizedWhitePeplicas();
            var specificTimeout = timeout.TotalMilliseconds / ReplicaAddresses.Length;
            
            foreach (var uri in shuffledAddresses)
            {
                var guid = Guid.NewGuid();
                var requestTask = WebRequestTask(uri, query, guid);
                await Task.WhenAny(requestTask, Task.Delay((int)specificTimeout));

                if (requestTask.IsCompleted && !requestTask.IsFaulted)
                    return requestTask.Result;
                                
                ClusterManager.MarkGrey(uri, query, TimeSpan.FromMilliseconds(500));
            }            

            throw new TimeoutException();            
        }

        protected override ILog Log => LogManager.GetLogger(typeof(RoundRobinClusterClient));
    }
}