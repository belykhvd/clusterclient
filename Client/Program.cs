using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
                var clients = new[] {new SimpleClient(replicaAddresses)};

                foreach (var client in clients)
                {
                    Log.Info($"Testing {client.GetType()} started");

                    Task.WaitAll(queries.Select(
                        async query =>
                        {
                            var timer = Stopwatch.StartNew();
                            try
                            {
                                await client.ProceedRequestAsync(query, TimeSpan.FromMilliseconds(1000));

                                Log.Info($"Query processed [{query}] in {timer.ElapsedMilliseconds} ms.");
                            }
                            catch (TimeoutException)
                            {
                                Log.Warn($"Query [{query}] timeout ({timer.ElapsedMilliseconds} ms).");
                            }
                        }).ToArray());

                    Log.Info($"Testing {client.GetType()} finished");
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e);
            }
        }
    }
}