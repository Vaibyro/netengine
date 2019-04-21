using NetEngineCore.Messaging;

namespace NetEngineServer.Filtering {
    /// <summary>
    /// Builtin filter to check authentication for each message.
    /// </summary>
    public class AuthenticationFilter : IFilter {
        public bool Filter(Server server, Message message) {
            return !(server.AuthenticationMandatory && message.NeedAuthentication &&
                   server.IsAuthenticated(message.ConnectionId));
        }
    }
}