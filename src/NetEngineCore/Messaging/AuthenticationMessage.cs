using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetEngineCore.Messaging {
    [MessagePackObject]
    public class AuthenticationMessage : Message {
        [Key(0)]
        public string Username { get; set; }

        [Key(1)]
        public string Password { get; set; }
    }
}