// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using log4net;

namespace Client.Clients
{
    public class RandomClusterClient : ClusterClientBase
    {
        private readonly Random randomGenerator = new Random();

        public RandomClusterClient(IEnumerable<string> replicaAddresses) : base(replicaAddresses)
        {
        }

        public override async Task<string> ProceedRequestAsync(string query, TimeSpan timeout)
        {
            var whiteReplica = ClusterManager.EnumerateWhiteReplica().ToArray();
            var uri = whiteReplica[randomGenerator.Next(whiteReplica.Length)];
            var guid = Guid.NewGuid();
                        
            var requestTask = WebRequestTask(uri, query, guid);
            await Task.WhenAny(requestTask, Task.Delay(timeout));

            if (requestTask.IsCompleted && !requestTask.IsFaulted)
                return requestTask.Result;

            ClusterManager.MarkGrey(uri, query, TimeSpan.FromMilliseconds(500));
            throw new TimeoutException();
        }

        protected override ILog Log => LogManager.GetLogger(typeof(RandomClusterClient));
    }
}