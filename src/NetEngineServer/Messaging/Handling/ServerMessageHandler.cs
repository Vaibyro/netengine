
using NetEngineCore.Messaging;
using NetEngineCore.Messaging.Handling;

namespace NetEngineServer.Messaging.Handling {
    public abstract class ServerMessageHandler<T> : MessageHandler<T> where T : IMessage {
        protected readonly Server Server;
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="server"></param>
        public ServerMessageHandler(Server server) {
            Server = server;
        }
        
        /// <summary>
        /// Protected method for message processing.
        /// </summary>
        /// <param name="message"></param>
        protected abstract void ProcessMessage(T message);

        /// <summary>
        /// Handle the message.
        /// </summary>
        /// <param name="message"></param>
        public override void Handle(IMessage message) {
            // Otherwise, it is okay to process the message
            ProcessMessage((T) message);
        }
    }
}