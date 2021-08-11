using Newtonsoft.Json;
using SimpleSecureWebsockets.API;
using SimpleSecureWebsockets.API.Packets;
using SimpleSecureWebsockets.Entities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleSecureWebsockets
{
    public sealed class WebsocketServer : IDisposable
    {
        /// <summary>
        ///     Fired when a new client connects.
        /// </summary>
        public event Func<WebsocketUser, Task> ClientConnected;
        
        /// <summary>
        ///     Fired when a client disconnects.
        /// </summary>
        public event Func<WebsocketUser, Task> ClientDisconnected;

        /// <summary>
        ///     Fired when a client resumes their connection.
        /// </summary>
        public event Func<WebsocketUser, Task> ClientResumed;

        /// <summary>
        ///     Fired when a client sends an event.
        /// </summary>
        public event Func<WebsocketUser, string, PayloadResolver, Task> EventReceived;

        /// <summary>
        ///     Gets a collection of connected clients.
        /// </summary>
        public IReadOnlyCollection<WebsocketUser> Clients
            => _clients.ToImmutableArray();

        internal readonly ServerConfig Config;

        private List<WebsocketUser> _clients;

        private readonly Func<string, Task<ulong?>> _checkAuth;

        private HttpServer server;

        private bool disposed = false;

        private WebsocketServer(ServerConfig config)
        {
            server = new HttpServer(config, this);
            server.Start(CancellationToken.None);
        }

        /// <summary>
        ///     Creates a new websocket server.
        /// </summary>
        /// <param name="authAsync">
        ///     Gets or sets the asynchronous function used to authenticate users. This function will be fired when the server receives a 
        ///     handshake packet, the passed in string is the authentication string.
        ///  <br/><br/>
        ///     If you would like to use a non async version, please use <see cref="WebsocketServer.WebsocketServer(Func{string, bool})"/>.
        /// </param>
        /// <param name="config">
        ///     The configuration of the server.
        /// </param>
        public WebsocketServer(Func<string, Task<ulong?>> authAsync, ServerConfig config)
            : this(config)
        {
            _checkAuth = authAsync;

            this.Config = config;
        }

        /// <summary>
        ///     Creates a new websocket server.
        /// </summary>
        /// <param name="auth">
        ///     Gets or sets the function used to authenticate users. This function will be fired when the server receives a 
        ///     handshake packet, the passed in string is the authentication string.
        ///  <br/><br/>
        ///     If you would like to use an async version, please use <see cref="WebsocketServer.WebsocketServer(Func{string, Task{bool}}, ServerConfig)"/>.
        /// </param>
        /// <param name="config">
        ///     The configuration of the server.
        /// </param>
        public WebsocketServer(Func<string, ulong?> auth, ServerConfig config)
            : this(config)
        {
            _checkAuth = (s) => Task.FromResult<ulong?>(auth(s));

            this.Config = config;
        }

        internal async Task HandleDisconnect(WebsocketUser user, string reason = null)
        {
            await user.DisconnectAsync(reason);

            // they might be switching pages, lets wait 5 seconds before invoking the remove event and removing them from the list
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);

                if (!user.Connected)
                {
                    if(_clients.Remove(user))
                        ClientDisconnected.DispatchEvent(user);
                }
            });
        }

        internal async Task HandleConnectionAsync(HttpListenerWebSocketContext context)
        {
            var socket = context.WebSocket;

            byte[] buff = new byte[1024];

            var resultTask = socket.ReceiveAsync(buff, CancellationToken.None);
            var delay = Task.Delay(5000);

            var t = await Task.WhenAny(resultTask, delay);

            if(delay == t)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "handshake timeout", CancellationToken.None);
                return;
            }

            var result = await resultTask;

            if(result.MessageType != WebSocketMessageType.Text)
            {
                await socket.CloseAsync(WebSocketCloseStatus.ProtocolError, null, CancellationToken.None);
                return;
            }


            var frame = SocketFrame.FromBuffer(buff);

            if(frame.OpCode != OpCodes.Handshake)
            {
                await socket.CloseAsync(WebSocketCloseStatus.ProtocolError, null, CancellationToken.None);
                return;
            }

            var handshake = frame.PayloadAs<Handshake>();

            var userId = await _checkAuth(handshake.Authentication);

            if (!userId.HasValue)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Unauthorized", CancellationToken.None);
                return;
            }

            await ResumeOrCreateSocket(socket, handshake, userId.Value);
        }

        private async Task ResumeOrCreateSocket(WebSocket socket, Handshake handshake, ulong userId)
        {
            var user = _clients.FirstOrDefault(x => x.UserId == userId);

            if (user != null)
            {
                user.ResumeAsync(handshake, socket);
                await user.SendAsync(new SocketFrame(OpCodes.HandshakeResult, new HandshakeResult(true)), CancellationToken.None);
                ClientResumed.DispatchEvent(user);

            }
            else
            {
                var websocketUser = new WebsocketUser(socket, handshake, userId, this);
                _clients.Add(websocketUser);
                await user.SendAsync(new SocketFrame(OpCodes.HandshakeResult, new HandshakeResult(false)), CancellationToken.None);
                ClientConnected.DispatchEvent(websocketUser);
            }
        }

        internal void DispatchEvent(WebsocketUser usr, string msg, PayloadResolver res)
            => EventReceived.DispatchEvent(usr, msg, res);

        public void Dispose()
        {
            if (!disposed)
            {
                server.Dispose();
                _clients.ForEach(async x => await x.DisconnectAsync("Server shutdown"));
                this._clients = null;

                this.disposed = true;
            }
        }
    }
}
