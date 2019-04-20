using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEngineCore.Messaging;
using NetEngineServer;
using NetEngineServer.Messaging.Handling;

namespace NetEngineServerTest.Handlers {
    public class ExampleServerHandler : ServerMessageHandler<ExampleMessage> {
        public ExampleServerHandler(Server server) : base(server) {
        }

        protected override void ProcessMessage(ExampleMessage message) {
            Console.WriteLine(message.Content);
        }
    }
}