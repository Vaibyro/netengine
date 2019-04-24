using NetEngineCore.Messaging;

namespace NetEngineServer.Filtering {
    public interface IFilter {
        bool Filter(Server server, IMessage message);
    }
}