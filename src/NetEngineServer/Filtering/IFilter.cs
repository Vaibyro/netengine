using NetEngineCore.Messaging;

namespace NetEngineServer.Filtering {
    public interface IFilter {
        bool Filter(Server server, Message message);
    }
}