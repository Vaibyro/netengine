using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEngineCore.Messaging;
using NetEngineCore.Networking;
using NetEngineServer;
using NetEngineServer.Messaging.Handling;
using Server = NetEngineServer.Server;

namespace NetEngineServerTest.Handlers {
    public class AuthenticationHandler : ServerMessageHandler<AuthenticationMessage> {
        public AuthenticationHandler(Server server) : base(server) {
        }

        protected override void ProcessMessage(AuthenticationMessage message) {
            // todo: temporary
            Console.WriteLine("[Auth Handler] Received auth! : " + message.Username + " / " + message.Password);

            // ...
            // Do the authentication process here...
            // ...
            
            Server.AuthenticateClient(message.ConnectionId, message.Username);
        }
    }
} 