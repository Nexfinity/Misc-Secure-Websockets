using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleSecureWebsockets
{
    internal class HttpServer : IDisposable
    {
        private HttpListener listener;

        private CancellationToken _cancelToken;

        private WebsocketServer server;

        public HttpServer(ServerConfig config, WebsocketServer server)
        {
            listener = new HttpListener();

            this.server = server;

#if DEBUG
            listener.Prefixes.Add($"http://localhost:{config.Port}{config.Path}");
#else
            listener.Prefixes.Add($"http://*:{config.Port}{config.Path}");
#endif


        }

        public void Start(CancellationToken token)
        {
            _cancelToken = token;
            listener.Start();
            _ = Task.Run(async () => await ListenerLoop());
        }

        public void Stop()
        {
            listener.Stop();
            _cancelToken = new CancellationToken(true);
        }

        private async Task ListenerLoop()
        {
            while (listener.IsListening)
            {
                _cancelToken.ThrowIfCancellationRequested();

                var context = await listener.GetContextAsync();

                _ = Task.Run(async () =>
                {
                    if (context.Request.IsWebSocketRequest)
                    {
                        var websocketContext = await context.AcceptWebSocketAsync(null);

                        await server.HandleConnectionAsync(websocketContext);
                    }
                });
            }
        }

        public void Dispose()
        {
            this.Stop();
            listener = null;
        }
    }
}
