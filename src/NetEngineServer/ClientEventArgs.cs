using System;

namespace NetEngineServer {
    public class ClientEventArgs : EventArgs {
        public Client Client { get; private set; }

        public ClientEventArgs(Client client) {
            Client = client;
        }
    }
}