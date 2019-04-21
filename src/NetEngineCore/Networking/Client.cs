using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using NetEngineCore.Networking.Exceptions;

namespace NetEngineCore.Networking {
    public class Client : Common {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public TcpClient client;
        Thread _receiveThread;
        Thread _sendThread;

        // TcpClient.Connected doesn't check if socket != null, which
        // results in NullReferenceExceptions if connection was closed.
        // -> let's check it manually instead
        public bool Connected => client != null &&
                                 client.Client != null &&
                                 client.Client.Connected;

        // TcpClient has no 'connecting' state to check. We need to keep track
        // of it manually.
        // -> checking 'thread.IsAlive && !Connected' is not enough because the
        //    thread is alive and connected is false for a short moment after
        //    disconnecting, so this would cause race conditions.
        // -> we use a threadsafe bool wrapper so that ThreadFunction can remain
        //    static (it needs a common lock)
        // => Connecting is true from first Connect() call in here, through the
        //    thread start, until TcpClient.Connect() returns. Simple and clear.
        // => bools are atomic according to
        //    https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/variables
        //    made volatile so the compiler does not reorder access to it
        volatile bool _connecting;
        public bool Connecting => _connecting;

        // send queue
        // => SafeQueue is twice as fast as ConcurrentQueue, see SafeQueue.cs!
        SafeQueue<byte[]> sendQueue = new SafeQueue<byte[]>();

        // ManualResetEvent to wake up the send thread. better than Thread.Sleep
        // -> call Set() if everything was sent
        // -> call Reset() if there is something to send again
        // -> call WaitOne() to block until Reset was called
        ManualResetEvent sendPending = new ManualResetEvent(false);

        // the thread function
        void ReceiveThreadFunction(string ip, int port) {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try {
                // connect (blocking)
                client.Connect(ip, port);
                _connecting = false;

                // start send thread only after connected
                _sendThread = new Thread(() => { SendLoop(0, client, sendQueue, sendPending); });
                _sendThread.IsBackground = true;
                _sendThread.Start();

                // run the receive loop
                ReceiveLoop(0, client, receiveQueue, MaxMessageSize);
            } catch (SocketException exception) {
                // this happens if (for example) the ip address is correct
                // but there is no server running on that ip/port
                // add 'Disconnected' event to message queue so that the caller
                // knows that the Connect failed. otherwise they will never know
                receiveQueue.Enqueue(new Packet(0, PacketType.Disconnection, null));
                throw new Exception("Client Recv: failed to connect to ip=" + ip + " port=" + port + " reason=" +
                                    exception);
            } catch (Exception exception) {
                // something went wrong. probably important.
                throw new Exception("Client Recv Exception: " + exception);
            }

            // sendthread might be waiting on ManualResetEvent,
            // so let's make sure to end it if the connection
            // closed.
            // otherwise the send thread would only end if it's
            // actually sending data while the connection is
            // closed.
            _sendThread?.Interrupt();

            // Connect might have failed. thread might have been closed.
            // let's reset connecting state no matter what.
            _connecting = false;

            // if we got here then we are done. ReceiveLoop cleans up already,
            // but we may never get there if connect fails. so let's clean up
            // here too.
            client.Close();
        }

        public void Connect(string ip, int port) {
            // not if already started
            if (Connecting || Connected) {
                return;
            }

            // We are connecting from now until Connect succeeds or fails
            _connecting = true;

            // TcpClient can only be used once. need to create a new one each
            // time.
            client = new TcpClient();
            client.NoDelay = NoDelay;
            client.SendTimeout = SendTimeout;

            // clear old messages in queue, just to be sure that the caller
            // doesn't receive data from last time and gets out of sync.
            // -> calling this in Disconnect isn't smart because the caller may
            //    still want to process all the latest messages afterwards
            receiveQueue = new ConcurrentQueue<Packet>();
            sendQueue.Clear();

            // client.Connect(ip, port) is blocking. let's call it in the thread
            // and return immediately.
            // -> this way the application doesn't hang for 30s if connect takes
            //    too long, which is especially good in games
            // -> this way we don't async client.BeginConnect, which seems to
            //    fail sometimes if we connect too many clients too fast
            _receiveThread = new Thread(() => { ReceiveThreadFunction(ip, port); });
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
        }

        public void Disconnect() {
            // only if started
            if (Connecting || Connected) {
                // close client
                client.Close();

                // wait until thread finished. this is the only way to guarantee
                // that we can call Connect() again immediately after Disconnect
                _receiveThread?.Join();

                // clear send queues. no need to hold on to them.
                // (unlike receiveQueue, which is still needed to process the
                //  latest Disconnected message, etc.)
                sendQueue.Clear();

                // let go of this one completely. the thread ended, no one uses
                // it anymore and this way Connected is false again immediately.
                client = null;
            }
        }

        public bool Send(byte[] data) {
            if (Connected) {
                // respect max message size to avoid allocation attacks.
                if (data.Length <= MaxMessageSize) {
                    // add to send queue and return immediately.
                    // calling Send here would be blocking (sometimes for long times
                    // if other side lags or wire was disconnected)
                    sendQueue.Enqueue(data);
                    sendPending.Set(); // interrupt SendThread WaitOne()
                    return true;
                }

                throw new OverSizedMessageException("Message too big: " + data.Length + ". Limit: " + MaxMessageSize);
            }

            throw new LostConnectionException("Client lost connection to the server");
        }
    }
}