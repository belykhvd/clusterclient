using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Client.Dependencies;
using log4net;

namespace Client.Clients
{
    public abstract class ClusterClientBase
    {
        protected readonly string[] ReplicaAddresses;
        protected readonly ClusterManager ClusterManager;

        protected ClusterClientBase(IEnumerable<string> replicaAddresses)
        {
            ReplicaAddresses = replicaAddresses.ToArray();
            ClusterManager = new ClusterManager(ReplicaAddresses);
        }

        public abstract Task<string> ProceedRequestAsync(string query, TimeSpan timeout);
        protected abstract ILog Log { get; }

        protected static HttpWebRequest CreateRequest(string uri)
        {
            var request = WebRequest.CreateHttp(Uri.EscapeUriString(uri));
            request.Proxy = null;
            request.KeepAlive = true;
            request.ServicePoint.UseNagleAlgorithm = false;
            request.ServicePoint.ConnectionLimit = 100500;
            return request;
        }

        protected async Task<string> ProceedRequestAsync(WebRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            using (var response = await request.GetResponseAsync())
            {
                var responseStream = response.GetResponseStream();
                var result = await new StreamReader(responseStream, Encoding.UTF8).ReadToEndAsync();

                var responseTime = stopwatch.ElapsedMilliseconds;
                /*if (result.Length != 0)
                    Log.Info($"Response from [{request.RequestUri.Port},{request.RequestUri.Query}] received in {responseTime} ms.");*/
                await RebalanceReplica(request.RequestUri, TimeSpan.FromMilliseconds(responseTime));
                
                return result;
            }
        }

        protected async Task ProceedCancelRequest(string uri, Guid cancelGuid)
        {
            var cancelRequest = CreateRequest($"{uri}?cancel={cancelGuid}");
            cancelRequest.GetResponseAsync();
            Log.Info($"Cancel request sent [{uri}, {cancelGuid}].");
        }

        private async Task RebalanceReplica(Uri requestUri, TimeSpan responseTimeSpan)
        {
            var replicaUri = $"{requestUri.Scheme}://{requestUri.Authority}{requestUri.LocalPath}";
            ClusterManager.Rebalance(replicaUri, responseTimeSpan);            
        }

        protected Task<string> WebRequestTask(string uri, string query, Guid guid)
        {
            var webRequest = CreateRequest($"{uri}?query={query}&guid={guid}");
            Log.Info($"Processing request [{query}] to [{uri}].");

            return ProceedRequestAsync(webRequest);
        }
    }
}