using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleSecureWebsockets
{
    public class ServerConfig
    {
        /// <summary>
        ///     Gets or sets the port that the server is running on. Defaults to 8000
        /// </summary>
        public int Port { get; set; } = 8000;

        /// <summary>
        ///     Gets or sets the path to the socket endpoint. ex: /api/socket would be ws://localhost:port/api/socket
        /// </summary>
        public string Path { get; set; } = "/";

        /// <summary>
        ///     Gets or sets the heartbeat interval in miliseconds.
        /// </summary>
        public int HeartbeatInterval { get; set; } = 30000;
    }
}
