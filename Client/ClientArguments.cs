using System;
using System.IO;
using Fclp;

namespace Client
{    
    internal class ClientArguments
    {        
        /*public string[] ReplicaAddresses { get; set; }
        public int TimeoutMilliseconds { get; set; }

        private string addressesFilename { get; set; }*/

        /*public static bool TryGetArguments(string[] args, out ClientArguments parsedArguments)
        {
            var argumentsParser = new FluentCommandLineParser<ClientArguments>();
            argumentsParser.Setup(a => a.TimeoutMilliseconds).As('t', "timeout").Required();
            
            argumentsParser.Setup(a => a.addressesFilename).As('f', "file")
                .WithDescription("Path to the file with replica addresses").Required();
            

            var addresses = default(string[]);
            argumentsParser.Setup<string>('f', "file")
                .WithDescription("Path to the file with replica addresses")
                .Callback(fileName => addresses = File.ReadAllLines(fileName))
                .Required();
        }*/

        public static bool TryGetReplicaAddresses(string[] args, out string[] replicaAddresses)
        {
            var argumentsParser = new FluentCommandLineParser();
            var result = default(string[]);

            argumentsParser.Setup<string>('f', "file")
                .WithDescription("Path to the file with replica addresses")
                .Callback(fileName => result = File.ReadAllLines(fileName))
                .Required();            

            argumentsParser.SetupHelp("?", "h", "help")
                .Callback(text => Console.WriteLine(text));

            var parsingResult = argumentsParser.Parse(args);
            if (parsingResult.HasErrors)
            {
                argumentsParser.HelpOption.ShowHelp(argumentsParser.Options);
                replicaAddresses = null;
                return false;
            }

            replicaAddresses = result;
            return !parsingResult.HasErrors;
        }
    }
}