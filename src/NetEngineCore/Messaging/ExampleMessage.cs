﻿using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetEngineCore.Messaging {
    [MessagePackObject]
    public class ExampleMessage : Message {
        [Key(0)]
        public string Content { get; set; }
    }
}