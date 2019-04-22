using NetEngineCore.Messaging;

namespace NetEngineServer.Filtering {
    /// <summary>
    /// Builtin filter to check authentication for each message.
    /// </summary>
    public class AuthenticationFilter : IFilter {
        public bool Enable { get; set; } = true;
        public bool Filter(Server server, Message message) {
            var block = (server.UseAuthentication && !(message is AuthenticationMessage) &&
                          server.IsAuthenticated(message.ConnectionId));

            if (block) {
                // todo send response bad authentication
            }
            
            return !block;
        }

        public void ResponseBadAuthentication(Server server, Client client) {
            // Logic when client is trying to cheat...
            var message = new ExampleMessage {Content = "You tried to send a packet without authentication."};
            //todo: send to client
        }
    }
}