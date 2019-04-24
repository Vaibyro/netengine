using NetEngineCore.Messaging;
using NetEngineServer;
using NetEngineServer.Filtering;

namespace NetEngineServerTest.Filters {
    public class ExampleFilter : IFilter {
        public bool Filter(Server server, IMessage message) {
            return true;
        }
    }
}