using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using log4net;

namespace Client.Clients
{
    public class BroadcastClient : ClientBase
    {
        public BroadcastClient(IEnumerable<string> replicaAddresses) : base(replicaAddresses)
        {
        }

        public override Task<string> ProceedRequestAsync(string query, TimeSpan timeout)
        {
            throw new NotImplementedException();
        }
        
        protected override ILog Log => LogManager.GetLogger(typeof(BroadcastClient));
    }
}