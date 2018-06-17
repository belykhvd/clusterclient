using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Client.Clients;
using log4net;
using log4net.Config;

namespace Client
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            if (!ClientArguments.TryGetReplicaAddresses(args, out var replicaAddresses))
            {
                Console.WriteLine("Faulted: Pass arguments to preconfigurate client.");
                return;
            }

            try
            {                
                var queries = File.ReadAllLines("Queries.txt");
                var clients = new ClusterClientBase[]
                {
                    new RandomClusterClient(replicaAddresses),
                    new BroadcastClusterClient(replicaAddresses),
                    new RoundRobinClusterClient(replicaAddresses),
                    new SmartClusterClient(replicaAddresses)
                };

                foreach (var client in clients)
                {
                    Log.Debug($"Testing {client.GetType()} started.");

                    Task.WaitAll(queries.Select(
                        async query =>
                        {
                            var timer = Stopwatch.StartNew();
                            try
                            {
                                await client.ProceedRequestAsync(query, TimeSpan.FromMilliseconds(500));

                                Log.Info($"Query processed [{query}] in {timer.ElapsedMilliseconds} ms.");
                            }
                            catch (TimeoutException)
                            {
                                Log.Warn($"Query [{query}] timeout ({timer.ElapsedMilliseconds} ms).");
                            }
                        }).ToArray());

                    Log.Debug($"Testing {client.GetType()} finished.");
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e);
            }
        }
    }
}