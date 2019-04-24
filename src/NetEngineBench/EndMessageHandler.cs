using System;
using NetEngineCore.Messaging;
using NetEngineCore.Messaging.Handling;
using NetEngineServer;
using NetEngineServer.Messaging.Handling;

namespace NetEngineBench {
    public class BenchMessageHandler : ServerMessageHandler<ExampleMessage> {

        public event EventHandler BenchFinished = delegate { };
        
        public int FinishedCount { get; set; }

        private int _currentCount = 0;
        
        protected override void ProcessMessage(ExampleMessage message) {
            if (message.Content == "EOF") {
                _currentCount++;
                if (_currentCount == FinishedCount) {
                    BenchFinished(this, new EventArgs());
                }
            }
        }

        public BenchMessageHandler(Server server) : base(server) {
        }
    }
}