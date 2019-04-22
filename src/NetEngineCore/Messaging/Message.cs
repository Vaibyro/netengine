using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetEngineCore.Messaging {
    [MessagePack.Union(0, typeof(ExampleMessage))]
    [MessagePack.Union(1, typeof(AuthenticationMessage))]
    public abstract class Message {
        [IgnoreMember]
        public int ConnectionId { get; set; }
    }
}