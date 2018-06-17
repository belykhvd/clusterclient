// ReSharper disable InconsistentNaming

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using log4net;

namespace Client.Dependencies
{
    public enum ReplicaStatus
    {
        Grey,
        White
    }

    public class ClusterManager
    {
        private readonly ConcurrentDictionary<string, int> replicaDelays = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, ReplicaStatus> replicaStatuses = new ConcurrentDictionary<string, ReplicaStatus>();

        public ClusterManager(IEnumerable<string> replicaAddresses)
        {
            var addresses = replicaAddresses.ToArray();            
            foreach (var uri in addresses)
            {
                replicaStatuses[uri] = ReplicaStatus.White;
                replicaDelays[uri] = int.MaxValue;
            }
        }

        public IEnumerable<string> EnumerateWhiteReplica()
            => replicaStatuses.Where(pair => pair.Value == ReplicaStatus.White).Select(pair => pair.Key);

        public IEnumerable<string> EnumerateWhiteReplicaRandomly()
            => EnumerateWhiteReplica().OrderBy(uri => Guid.NewGuid());

        public IEnumerable<string> EnumerateWhiteReplicaByPriority()
            => EnumerateWhiteReplica().OrderBy(uri => replicaDelays[uri]);

        public IEnumerable<string> ShuffledPrioritizedWhitePeplicas()
        {            
            var shuffledPrioritized = EnumerateWhiteReplicaByPriority()
                .Split(3)
                .SelectMany(group => group.OrderBy(uri => Guid.NewGuid()))
                .ToArray();
            //Log.Info(string.Join(" ", shuffledPrioritized.Select(uri => replicaDelays[uri])));
            return shuffledPrioritized;
        }

        public async void MarkGrey(string uri, string query, TimeSpan delay)
        {            
            if (replicaStatuses[uri] == ReplicaStatus.Grey)
                return;

            replicaStatuses[uri] = ReplicaStatus.Grey;
            Log.Warn($"Replica [{uri}] marked grey for {delay.Milliseconds} ms by [{query}].");

            await Task.Delay(delay);
            replicaStatuses[uri] = ReplicaStatus.White;
        }

        public void Rebalance(string replicaUri, TimeSpan responseTime)
        {
            replicaDelays[replicaUri] = (int) responseTime.TotalMilliseconds;
            Log.Debug($"Responsed [{replicaUri}] in {responseTime.TotalMilliseconds} ms.");
        }
            

        private static ILog Log => LogManager.GetLogger(typeof(ClusterManager));
    }
}