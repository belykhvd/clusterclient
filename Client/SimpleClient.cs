using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using log4net;

namespace Client
{
    public class SimpleClient : ClientBase
    {
        public SimpleClient(IEnumerable<string> replicaAddresses) : base(replicaAddresses)
        {
        }
      
        public override async Task<string> ProceedRequestAsync(string query, TimeSpan timeout)
        {                        
            var uri = ReplicaAddresses[0];
            var guid = Guid.NewGuid();
            var webRequest = CreateRequest($"{uri}?query={query}&guid={guid}");

            var resultTask = ProceedRequestAsync(webRequest);
            await Task.WhenAny(resultTask, Task.Delay(timeout));

            if (!resultTask.IsCompleted || resultTask.IsFaulted)
                throw new TimeoutException();
            
            return resultTask.Result;
        }

        protected override ILog Log => LogManager.GetLogger(typeof(SimpleClient));
    }
}