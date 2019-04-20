using NetEngineCore.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEngineCore.Networking;

namespace NetEngineServer {
    public class Client {
        /// <summary>
        /// Get or set address of the client.
        /// </summary>
        public string Address { get; }
        
        /// <summary>
        /// Get or set id of the client.
        /// </summary>
        public int Id { get; }
        
        /// <summary>
        /// Get or set identifier of the client.
        /// </summary>
        public string Identifier { get; set; }
        
        /// <summary>
        /// Get if the client is authenticated.
        /// </summary>
        public bool Authenticated { get; set; } = false;
        
        /// <summary>
        /// Get the associated game server.
        /// </summary>
        public Server Server { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="address"></param>
        /// <param name="server"></param>
        public Client(int id, string address, Server server) {
            Address = address;
            Id = id;
            Server = server;
        }

        public void Send(byte[] message) {
            Server.NetworkingServer.Send(Id, message);
        }

        public void Send(Packet packet) {
            Send(packet.Data);
        }
        
        public void Send(Message message) {
            var binaryMessage = MessagePack.MessagePackSerializer.Serialize(message);
            Send(binaryMessage);
        }
        
        
        
        public override string ToString() {
            return string.Format("{0} ({1})", Id, Address);
        }
    }
}