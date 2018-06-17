using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;

namespace Server
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            if (!ServerArguments.TryGetArguments(args, out var arguments))
            {
                Console.WriteLine("Faulted: Pass arguments to preconfigurate server.");
                return;
            }

            var listener = new HttpListener()
            {
                Prefixes = {$"http://+:{arguments.Port}/{arguments.MethodName}/"}
            };

            Log.Info($"Server is starting listening prefixes: {string.Join("; ", listener.Prefixes)}.");
            Log.Info("Press <ENTER> to stop listening.");

            listener.ProceedRequestsAsync(CreateAsyncCallback(arguments));

            Console.ReadLine();
            Log.Info("Server stopped.");
        }

        /* Two scenarios: 'query'[+'guid'] or 'cancel' requests. */
        private static Func<HttpListenerContext, Task> CreateAsyncCallback(ServerArguments serverArguments)
        {
            return async context =>
            {
                var queryString = context.Request.QueryString;
                if (queryString["query"] != null)
                {
                    Log.Info($"Receive request [{queryString["query"]}].");

                    var guidString = queryString["guid"];
                    if (guidString != null)
                    {
                        if (Guid.TryParse(guidString, out var clientGuid))
                        {
                            var guidCancellationTokenSource = new CancellationTokenSource();
                            var guidCancellationToken = guidCancellationTokenSource.Token;

                            TaskCancellationTokenSources[clientGuid] = guidCancellationTokenSource;
                            try
                            {
                                await ComputationalAsyncCallback(serverArguments, context, guidCancellationToken);
                            }
                            catch (OperationCanceledException)
                            {
                                Log.Warn($"Task [{queryString["query"]}] cancelled while or after computing/sending response.");
                            }
                            catch (HttpListenerException)
                            {
                                Log.Warn("Response writing was aborted.");
                            }
                        }
                        else Log.Warn($"Bad request: guid [{guidString}] cannot be parsed.");
                    }                    
                    else Log.Warn("Bad request: no guid passed.");
                }
                else if (queryString["cancel"] != null)
                    CancellationAsyncCallback(serverArguments, context);
                else
                    Log.Warn("Bad request: neither query nor cancel.");
            };
        }

        private static async Task ComputationalAsyncCallback(ServerArguments serverArguments, 
            HttpListenerContext context, CancellationToken cancellationToken)
        {            
            var profiler = Stopwatch.StartNew();
            
            var query = context.Request.QueryString["query"];

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Warn($"Task [{query}] cancelled before computations.");
                return;
            }
            
            var responseBytes = await ComputeResponseAsync(serverArguments, query, cancellationToken);
            await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Warn($"Task [{query}] cancelled while or after computing/sending response.");
                return;
            }

            Log.Info($"Request [{query}] processed in {profiler.ElapsedMilliseconds} ms.");
        }
        
        private static void CancellationAsyncCallback(ServerArguments serverArguments, HttpListenerContext context)
        {
            var cancellationGuidString = context.Request.QueryString["cancel"];
            if (!Guid.TryParse(cancellationGuidString, out var cancelGuid))
            {
                Log.Warn($"Cancellation guid [{cancellationGuidString}] cannot be parsed.");
                return;
            }

            if (!TaskCancellationTokenSources.ContainsKey(cancelGuid))
            {
                Log.Warn($"Cancellation guid [{cancellationGuidString}] cannot be found.");
                return;
            }

            TaskCancellationTokenSources[cancelGuid].Cancel(false);
            TaskCancellationTokenSources[cancelGuid].Dispose();
            TaskCancellationTokenSources.TryRemove(cancelGuid, out var _);            
        }

        // TODO: guid from client now. it's bad solution - might be collistions.
        private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> TaskCancellationTokenSources =
            new ConcurrentDictionary<Guid, CancellationTokenSource>();

        #region Response Computation
        private static async Task<byte[]> ComputeResponseAsync(ServerArguments serverArguments, string query, 
            CancellationToken cancellationToken)
        {
            await Task.Delay(serverArguments.MethodDuration, cancellationToken);
            return GetBase64HashBytes(query, Encoding.UTF8);
        }

        private static byte[] GetBase64HashBytes(string query, Encoding encoding)
        {            
            using (var hasher = new HMACMD5(Key))
            {
                var hash = Convert.ToBase64String(hasher.ComputeHash(encoding.GetBytes(query ?? "")));
                return encoding.GetBytes(hash);
            }            
        }

        private static readonly byte[] Key = Encoding.UTF8.GetBytes("CLUSTER_CLIENT");
        #endregion
    }
}