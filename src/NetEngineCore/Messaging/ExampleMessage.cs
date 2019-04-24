using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetEngineCore.Messaging {
    [MessagePackObject]
    public class ExampleMessage : IMessage {
        [Key(0)]
        public string Content { get; set; }

        [IgnoreMember]
        public int ConnectionId { get; set; }

        public ExampleMessage(string content) {
            Content = content;
        }

        public ExampleMessage() {
            
        }
    }
}