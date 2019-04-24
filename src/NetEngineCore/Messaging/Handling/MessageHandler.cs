using NetEngineCore.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEngineCore.Networking;

namespace NetEngineCore.Messaging.Handling {
    public abstract class MessageHandler<T> : IMessageHandler where T : IMessage {
        /// <summary>
        /// Get the type of the processed message.
        /// </summary>
        /// <returns></returns>
        public Type GetMessageType() {
            return typeof(T);
        }

        /// <summary>
        /// Handle the message.
        /// </summary>
        /// <param name="message"></param>
        public abstract void Handle(IMessage message);
    }
}