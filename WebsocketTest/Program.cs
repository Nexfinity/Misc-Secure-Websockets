using SimpleSecureWebsockets;
using System;
using System.Threading.Tasks;

namespace WebsocketTest
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().StartAsync().GetAwaiter().GetResult();   
        }

        public async Task StartAsync()
        {
            var server = new WebsocketServer(CheckAuth, new ServerConfig());

            server.ClientConnected += Server_ClientConnected;

            server.ClientDisconnected += Server_ClientDisconnected;

            server.ClientResumed += Server_ClientResumed;

            await Task.Delay(-1);
        }

        private async Task Server_ClientResumed(SimpleSecureWebsockets.Entities.WebsocketUser arg)
        {
            Console.WriteLine($"Client with the user id: {arg.UserId} resumed connection");
        }

        private async Task Server_ClientDisconnected(SimpleSecureWebsockets.Entities.WebsocketUser arg)
        {
            Console.WriteLine($"Client with the user id: {arg.UserId} disconnected");

        }

        private async Task Server_ClientConnected(SimpleSecureWebsockets.Entities.WebsocketUser arg)
        {
            Console.WriteLine($"Client with the user id: {arg.UserId} connected");
        }

        public ulong? CheckAuth(string s)
        {
            return 1;
        }

        public async Task<ulong?> CheckAuthAsync(string s)
        {
            return 1;
        }
    }
}
