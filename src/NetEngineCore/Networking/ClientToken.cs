using System.Net.Security;
using System.Net.Sockets;
using System.Threading;

namespace NetEngineCore.Networking {
    // class with all the client's data. let's call it Token for consistency
    // with the async socket methods.
    public class ClientToken {
        public TcpClient Client { get; }

        // send queue
        // SafeQueue is twice as fast as ConcurrentQueue, see SafeQueue.cs!
        public SafeQueue<byte[]> sendQueue = new SafeQueue<byte[]>();

        // ManualResetEvent to wake up the send thread. better than Thread.Sleep
        // -> call Set() if everything was sent
        // -> call Reset() if there is something to send again
        // -> call WaitOne() to block until Reset was called
        public ManualResetEvent sendPending = new ManualResetEvent(false);
        
        public SslStream SslStream { get; set;  }
        
        public ClientToken(TcpClient client) {
            Client = client;
        }

        public ClientToken(TcpClient client, SslStream stream) {
            Client = client;
            SslStream = stream;
        }
    }
}