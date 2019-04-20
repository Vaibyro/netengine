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
            Console.WriteLine("Handling auth : " + message.Username + " " + message.Password);

            // Do the authentication process here...

            var connection = Server.GetClient(message.ConnectionId);
            connection.Identifier = message.Username;
            connection.Authenticated = true;
        }
    }
}