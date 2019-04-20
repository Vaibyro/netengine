using NetEngineCore.Messaging;
using NetEngineCore.Messaging.Handling;

namespace NetEngineClient.Messaging.Handling {
    public abstract class ClientMessageHandler<T> : MessageHandler<T> where T : Message {
        protected Client Client;
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="client"></param>
        public ClientMessageHandler(Client client) {
            Client = client;
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
            ProcessMessage(message as T);
        }
    }
}