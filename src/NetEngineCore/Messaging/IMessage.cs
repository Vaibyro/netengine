using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetEngineCore.Messaging {
    [MessagePack.Union(-2, typeof(ExampleMessage))]
    [MessagePack.Union(-1, typeof(AuthenticationMessage))]
    public interface IMessage {
        [IgnoreMember]
        int ConnectionId { get; set; }
    }
}