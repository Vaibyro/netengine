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
            Console.WriteLine("[AUTH HANDLER] Received auth! : " + message.Username + " / " + message.Password);

            if (!VerifyCredentials(message)) {
                OnBadAuthentication(message);
                return;
            }

            OnGoodAuthentication(message);
            Server.AuthenticateClient(message.ConnectionId, message.Username);
        }

        protected virtual bool VerifyCredentials(AuthenticationMessage message) {
            return false; //todo
        }

        protected virtual void OnBadAuthentication(AuthenticationMessage message) {
            Server.GetWaitingListClient(message.ConnectionId).Send(new ExampleMessage() {Content = "[SERVER] Bad credentials"});
        }

        protected virtual void OnGoodAuthentication(AuthenticationMessage message) {
            Server.GetWaitingListClient(message.ConnectionId).Send(new ExampleMessage() {Content = "[SERVER] You are authenticated"});
        }
    }
} 