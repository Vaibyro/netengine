using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetEngineCore.Networking {
    // Cote serveur
    class Connection {
        private Server _server;

        public Connection(Server server) {
            _server = server;
        }

        public int Id { get; set; }
        public string Ip { get; set; }
        public string Identifier { get; set; } // username
        public bool Authenticated { get; set; } = false;

        public bool TestCredentials() {
            // TODO
            return true;
        }

        public void Register() {
            bool isValid = TestCredentials();
            if (isValid) {
                Authenticated = true;
            } else {
                // close the connection if credentials are false
                Close();
            }
        }

        /// <summary>
        /// Close this connection.
        /// </summary>
        public void Close() {
            _server.Disconnect(Id);
        }
    }
}