using Newtonsoft.Json;
using SimpleSecureWebsockets.API;
using SimpleSecureWebsockets.API.Packets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleSecureWebsockets.Entities
{
    public class WebsocketUser
    {
        public event Func<Task> Closed;
        public event Func<string, PayloadResolver, Task> EventReceived;

        /// <summary>
        ///     Gets the authentication string used to authenticate this user.
        /// </summary>
        public string Authentication { get; private set; }

        /// <summary>
        ///     Gets the current page this user is on.
        /// </summary>
        public string Page { get; private set; }

        public IReadOnlyCollection<string> Events
            => _events.ToImmutableArray();

        public bool Connected { get; private set; }

        public readonly ulong UserId;

        private List<string> _events;

        private WebSocket socket;

        private TaskCompletionSource<bool> _resumeSource;

        private TaskCompletionSource<bool> _heartbeat;

        private readonly int heartbeatInterval;

        private readonly WebsocketServer server;

        public Task DispatchAsync(string eventName, object payload)
        {
            var dispatch = new Dispatch(eventName, payload);
            var frame = new SocketFrame(OpCodes.Dispatch, dispatch);
            return SendAsync(frame, CancellationToken.None);
        }

        public async Task DisconnectAsync(string reason = null)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
            this.Connected = false;
        }

        internal WebsocketUser(WebSocket socket, Handshake handshake, ulong userId, WebsocketServer server)
        {
            this.socket = socket;
            this.Authentication = handshake.Authentication;
            this._events = new List<string>(handshake.Events);
            this.Page = handshake.Page;
            this.UserId = userId;
            this._resumeSource = new TaskCompletionSource<bool>();
            this.heartbeatInterval = server.Config.HeartbeatInterval;
            this.server = server;
            _ = Task.Run(async () => await StartHeartbeatAsync());
        }

        internal void ResumeAsync(Handshake handshake, WebSocket socket)
        {
            this.socket = socket;
            this.Authentication = handshake.Authentication;
            this._events = new List<string>(handshake.Events);
            this.Page = handshake.Page;

            _resumeSource.SetResult(true);
            this._resumeSource = new TaskCompletionSource<bool>();
            _ = Task.Run(async () => await StartHeartbeatAsync());
        }

        private async Task CheckResume()
        {
            if(socket.State == WebSocketState.Closed)
            {
                var result = await _resumeSource.Task;

                if (!result)
                    throw new WebSocketException("The socket has closed");
            }
        }

        private async Task ReceiveLoop()
        {
            while (true)
            {
                if (!Connected)
                    return;

                await ReceiveLoop();
            }
        }

        private async Task ReceiveAsync()
        {
            while (Connected)
            {
                try
                {
                    await CheckResume();

                    byte[] buff = new byte[1024];

                    var data = await socket.ReceiveAsync(buff, CancellationToken.None);

                    switch (data.MessageType)
                    {
                        case WebSocketMessageType.Close:
                            // closes are handled by the websocket server.
                            await server.HandleDisconnect(this);
                            break;

                        case WebSocketMessageType.Text:
                            {
                                var frame = SocketFrame.FromBuffer(buff);

                                switch (frame.OpCode)
                                {
                                    case OpCodes.Dispatch:
                                        HandleDispatch(frame);
                                        break;
                                    case OpCodes.HeartbeatAck:
                                        _heartbeat.SetResult(true);
                                        break;
                                }
                            }
                            break;
                    }

                }
                catch (Exception x)
                {
                    Console.WriteLine(x);
                    await DisconnectAsync();
                }
            }
        }

        private async Task StartHeartbeatAsync()
        {
            while (true)
            {
                await Task.Delay(heartbeatInterval);

                if (!Connected)
                    return;

                _heartbeat = new TaskCompletionSource<bool>();

                async Task delayTask()
                {
                    await Task.Delay(5000);
                    if(!_heartbeat.Task.IsCompleted)
                        _heartbeat.SetResult(false);
                };

                var frame = new SocketFrame()
                {
                    OpCode = OpCodes.Heartbeat,
                    Packet = null
                };

                await SendAsync(frame, CancellationToken.None);

                _ = Task.Run(delayTask);
                var result = await _heartbeat.Task;

                if (!result)
                {
                    await DisconnectAsync("Failed heartbeat");
                    return;
                }
            }
        }

        private void HandleDispatch(SocketFrame frame)
        {
            var dispatch = frame.PayloadAs<Dispatch>();

            var resolver = new PayloadResolver(dispatch);

            EventReceived.DispatchEvent(dispatch.EventTarget, resolver);
            server.DispatchEvent(this, dispatch.EventTarget, resolver);
        }

        internal Task SendAsync(SocketFrame frame, CancellationToken token)
        {
            var json = frame.Json;

            var buffer = Encoding.UTF8.GetBytes(json);

            return socket.SendAsync(buffer, WebSocketMessageType.Text, true, token);
        }
    }
}
