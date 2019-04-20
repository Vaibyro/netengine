
using NetEngineCore.Messaging;
using NetEngineCore.Messaging.Handling;

namespace NetEngineServer.Messaging.Handling {
    public abstract class ServerMessageHandler<T> : MessageHandler<T> where T : Message {
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
        public override void Handle(Message message) {
            // If authentication is mandatory, the message cannot be received if the user is not authenticated.
            if (Server.AuthenticationMandatory && message.NeedAuthentication && !Server.GetClient(message.ConnectionId).Authenticated) {
                // Kick the user, maybe he is trying to cheat
                Server.ForceDisconnectClient(message.ConnectionId);
                return;
            }

            // Otherwise, it is okay to process the message
            ProcessMessage((T) message);
        }
    }
}