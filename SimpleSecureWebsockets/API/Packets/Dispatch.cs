using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleSecureWebsockets.API.Packets
{
    internal class Dispatch : IPacket
    {
        [JsonProperty("event")]
        public string EventTarget { get; set; }

        [JsonProperty("payload")]
        public JToken Payload { get; set; }

        public Dispatch() { }

        public Dispatch(string target, object payload)
        {
            this.EventTarget = target;
            this.Payload = JToken.FromObject(payload);
        }
    }
}
