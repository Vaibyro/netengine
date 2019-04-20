using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEngineClient;
using NetEngineClient.Messaging.Handling;
using NetEngineCore.Messaging;

namespace NetEngineClientTest.Handlers
{
    public class ExampleClientHandler : ClientMessageHandler<ExampleMessage>
    {
        public ExampleClientHandler(Client client) : base(client)
        {
        }

        protected override void ProcessMessage(ExampleMessage message)
        {
            Console.WriteLine(message.Content);
        }
    }
}
