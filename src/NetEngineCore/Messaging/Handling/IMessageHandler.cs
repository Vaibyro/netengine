using NetEngineCore.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetEngineCore.Messaging.Handling {
    public interface IMessageHandler {
        void Handle(Message message);
        Type GetMessageType();
    }
}