using MessagePack;
using NetEngineCore.Messaging;

namespace NetEngineBench {
    [MessagePack.Union(0, typeof(EndMessage))]
    public class Message : IMessage {
        [IgnoreMember]
        public int ConnectionId { get; set; }
    }
}