using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NetEngineCore.Messaging;
using NetEngineCore.Messaging.Dispatching;
using NetEngineCore.Messaging.Handling;

namespace NetEngineServer.Messaging.Dispatching {
    public class ServerMessageDispatcher : IMessageDispatcher{
        private Dictionary<Type, IMessageHandler> _handlers = new Dictionary<Type, IMessageHandler>();
        private readonly Server _server;

        public ServerMessageDispatcher(Server server) {
            _server = server;
        }

        public IMessageHandler GetHandler(Type messageType) {
            return _handlers[messageType];
        }

        public IEnumerable<IMessageHandler> GetHandlers() {
            return _handlers.Values;
        }

        public void AttachHandler(Type messageType, IMessageHandler handler) {
            _handlers.Add(messageType, handler); //todo: maybe verify type (if its message type)
        }

        public void DetachAllHandlers() {
            _handlers.Clear();
        }

        public void Dispatch(IMessage message) {
            if (_handlers.TryGetValue(message.GetType(), out IMessageHandler handler)) {
                handler.Handle(message);
            } else {
                throw new NotImplementedException("No handler found");
            }
        }
    }
}