using System;
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

            listener.ProceedRequestsAsync(ComputationalAsyncCallback(arguments));

            Console.ReadLine();
            Log.Info("Server stopped.");
        }

        /*private static Func<HttpListenerContext, Task> CreateAsyncCallback(ServerArguments serverArguments)
        {
            return async context =>
            {
                var queryString = context.Request.QueryString;
                if (queryString["query"] != null)
                    await ComputationalAsyncCallback(serverArguments, context);
            };
        }*/

        private static Func<HttpListenerContext, Task> ComputationalAsyncCallback(ServerArguments serverArguments)
        {
            return async context =>
            {
                var profiler = Stopwatch.StartNew();

                var query = context.Request.QueryString["query"];
                // var clientGuid = context.Request.QueryString["guid"];

                var responseBytes = await ComputeResponseAsync(serverArguments, query);
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);

                Log.Info($"Request [{query}] processed in {profiler.ElapsedMilliseconds} ms.");
            };            
        }     

        #region Response Computation
        private static async Task<byte[]> ComputeResponseAsync(ServerArguments serverArguments, string query)
        {
            //await Task.Delay(serverArguments.MethodDuration);
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