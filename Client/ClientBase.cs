using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace Client
{
    public abstract class ClientBase
    {
        protected readonly string[] ReplicaAddresses;        
        
        protected ClientBase(IEnumerable<string> replicaAddresses)
        {
            ReplicaAddresses = replicaAddresses.ToArray();            
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
            var profiler = Stopwatch.StartNew();
            using (var response = await request.GetResponseAsync())
            {
                var responseStream = response.GetResponseStream();
                var result = await new StreamReader(responseStream, Encoding.UTF8).ReadToEndAsync();

                var responseTime = profiler.ElapsedMilliseconds;
                if (result.Length != 0)
                    Log.Info($"Response from [{request.RequestUri.Port},{request.RequestUri.Query}] received in {responseTime} ms.");
                
                return result;
            }
        }

        protected async Task ProceedCancelRequest(string uri, Guid cancelGuid)
        {
            var cancelRequest = CreateRequest($"{uri}?cancel={cancelGuid}");
            cancelRequest.GetResponseAsync();
            Log.Info($"Cancel request sent [{uri}, {cancelGuid}].");
        }
    }
}