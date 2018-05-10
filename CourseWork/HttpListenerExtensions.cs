using System;
using System.Net;
using System.Threading.Tasks;
using log4net;

namespace Server
{
    public static class HttpListenerExtensions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HttpListenerExtensions));

        public static async Task ProceedRequestsAsync(this HttpListener listener, Func<HttpListenerContext, Task> callbackAsync)
        {
            listener.Start();

            while (true)
            {
                try
                {
                    var context = await listener.GetContextAsync();

                    Task.Run(async () =>
                    {                        
                        try
                        {
                            await callbackAsync(context);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                        finally
                        {
                            context.Response.Close();
                        }
                    });
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }        
    }
}