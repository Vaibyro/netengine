using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using NetEngineCore.Messaging.Handling;
using NetEngineCore.Networking;

namespace NetEngineCore.Messaging.Dispatching {
    public interface IMessageDispatcher {
        IMessageHandler GetHandler(Type messageType);
        IEnumerable<IMessageHandler> GetHandlers();
        void AttachHandler(Type messageType, IMessageHandler handler);
        void DetachAllHandlers();
        void Dispatch(Message message);
    }
}