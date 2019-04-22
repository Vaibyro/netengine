using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace NetEngineCore.Networking {
    public class Server : Common {
        
        #region Properties

        /// <summary>
        /// Check if the server is running.
        /// </summary>
        public bool Active => _listenerThread != null && _listenerThread.IsAlive;
        
        #endregion
        
        
        #region Private members
        
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private Thread _listenerThread;
        

        
        // clients with <connectionId, ClientData>
        private ConcurrentDictionary<int, ClientToken> _clients = new ConcurrentDictionary<int, ClientToken>();
         
        // connectionId counter
        // (right now we only use it from one listener thread, but we might have
        //  multiple threads later in case of WebSockets etc.)
        // -> static so that another server instance doesn't start at 0 again.
        private static int _counter;
        
        #endregion
        
        
        // listener
        public TcpListener listener;
        

        #region Constructors
        
        #endregion
        
        

       


        // public next id function in case someone needs to reserve an id
        // (e.g. if hostMode should always have 0 connection and external
        //  connections should start at 1, etc.)
        public static int NextConnectionId() {
            int id = Interlocked.Increment(ref _counter);

            // it's very unlikely that we reach the uint limit of 2 billion.
            // even with 1 new connection per second, this would take 68 years.
            // -> but if it happens, then we should throw an exception because
            //    the caller probably should stop accepting clients.
            // -> it's hardly worth using 'bool Next(out id)' for that case
            //    because it's just so unlikely.
            if (id == int.MaxValue) {
                throw new Exception("connection id limit reached: " + id);
            }

            return id;
        }

        /// <summary>
        /// Listener thread's listen function.
        /// </summary>
        /// <param name="port"></param>
        private void Listen(int port) {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try {
                // start listener
                listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
                listener.Server.NoDelay = NoDelay;
                listener.Server.SendTimeout = SendTimeout;
                listener.Start();
                logger.Info("Server: listening port=" + port);

                // keep accepting new clients
                while (true) {
                    // wait and accept new client
                    // note: 'using' sucks here because it will try to
                    // dispose after thread was started but we still need it
                    // in the thread
                    TcpClient client = listener.AcceptTcpClient();

                    // generate the next connection id (thread safely)
                    int connectionId = NextConnectionId();

                    // add to dict immediately
                    ClientToken token = new ClientToken(client);
                    _clients[connectionId] = token;
                    
                    
                    
                    if (UseSsl) {
                        token.SslStream = new SslStream(client.GetStream(), false, AcceptCertificate);

                        Console.WriteLine("SSL in use");
                        
                        // SSL auth
                        StartTls(token);
                    }
                    

                    // spawn a send thread for each client
                    Thread sendThread = new Thread(() => {
                        // wrap in try-catch, otherwise Thread exceptions
                        // are silent
                        try {
                            // set the stream
                            Stream currentStream;
                            if (UseSsl) {
                                currentStream = token.SslStream;
                            } else {
                                currentStream = client.GetStream();
                            }
                            
                            // run the send loop
                            SendLoop(connectionId, client, token.sendQueue, token.sendPending, currentStream);
                        } catch (ThreadAbortException) {
                            // happens on stop. don't log anything.
                            // (we catch it in SendLoop too, but it still gets
                            //  through to here when aborting. don't show an
                            //  error.)
                        } catch (Exception exception) {
                            logger.Error("Server send thread exception: " + exception);
                        }
                    });
                    sendThread.IsBackground = true;
                    sendThread.Start();

                    // spawn a receive thread for each client
                    Thread receiveThread = new Thread(() => {
                        // wrap in try-catch, otherwise Thread exceptions
                        // are silent
                        try {
                            // run the receive loop
                            ReceiveLoop(connectionId, client, ReceiveQueue, MaxMessageSize, client.GetStream());

                            // remove client from clients dict afterwards
                            _clients.TryRemove(connectionId, out ClientToken _);

                            // sendthread might be waiting on ManualResetEvent,
                            // so let's make sure to end it if the connection
                            // closed.
                            // otherwise the send thread would only end if it's
                            // actually sending data while the connection is
                            // closed.
                            sendThread.Interrupt();
                        } catch (Exception exception) {
                            logger.Error("Server client thread exception: " + exception);
                        }
                    });
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
            } catch (ThreadAbortException exception) {
                // UnityEditor causes AbortException if thread is still
                // running when we press Play again next time. that's okay.
                logger.Info("Server thread aborted. That's okay. " + exception);
            } catch (SocketException exception) {
                // calling StopServer will interrupt this thread with a
                // 'SocketException: interrupted'. that's okay.
                logger.Info("Server Thread stopped. That's okay. " + exception);
            } catch (Exception exception) {
                // something went wrong. probably important.
                logger.Error("Server Exception: " + exception);
            }
        }

        /// <summary>
        /// Start TLS server.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private bool StartTls(ClientToken client)
        {
            try
            {
                // the two bools in this should really be contruction paramaters
                // maybe re-use mutualAuthentication and acceptInvalidCerts ?
                client.SslStream.AuthenticateAsServer(SslCertificate, true, SslProtocols.Tls12, false);

                if (!client.SslStream.IsEncrypted)
                {
                    Console.WriteLine("*** StartTls stream from # not encrypted");
                    return false;
                }

                if (!client.SslStream.IsAuthenticated)
                {
                    Console.WriteLine("*** StartTls stream from # not authenticated");
                    return false;
                }

                if (!client.SslStream.IsMutuallyAuthenticated)
                {
                    Console.WriteLine("*** StartTls stream from # failed mutual authentication");
                    //client.Dispose();
                    return false;
                }
            }
            catch (IOException ex)
            {
                // Some type of problem initiating the SSL connection
                switch (ex.Message)
                {
                    case "Authentication failed because the remote party has closed the transport stream.":
                    case "Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.":
                        Console.WriteLine("*** StartTls IOException # closed the connection.");
                        break;
                    case "The handshake failed due to an unexpected packet format.":
                        Console.WriteLine("*** StartTls IOException # disconnected, invalid handshake.");
                        break;
                    default:
                        Console.WriteLine("*** StartTls IOException from # " + Environment.NewLine + ex.ToString());
                        break;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("*** StartTls Exception from # " + Environment.NewLine + ex.ToString());
                return false;
            }

            return true;
        }
        
        // start listening for new connections in a background thread and spawn
        // a new thread for each one.
        public bool Start(int port) {
            // not if already started
            if (Active) {
                return false;
            }

            // clear old messages in queue, just to be sure that the caller
            // doesn't receive data from last time and gets out of sync.
            // -> calling this in Stop isn't smart because the caller may
            //    still want to process all the latest messages afterwards
            ReceiveQueue = new ConcurrentQueue<Packet>();

            // start the listener thread
            // (on low priority. if main thread is too busy then there is not
            //  much value in accepting even more clients)
            logger.Info("Server: Start port=" + port);
            _listenerThread = new Thread(() => { Listen(port); });
            _listenerThread.IsBackground = true;
            _listenerThread.Priority = ThreadPriority.BelowNormal;
            _listenerThread.Start();
            return true;
        }

        public void Stop() {
            // only if started
            if (!Active) {
                return;
            }

            logger.Info("Server: stopping...");

            // stop listening to connections so that no one can connect while we
            // close the client connections
            // (might be null if we call Stop so quickly after Start that the
            //  thread was interrupted before even creating the listener)
            listener?.Stop();

            // kill listener thread at all costs. only way to guarantee that
            // .Active is immediately false after Stop.
            // -> calling .Join would sometimes wait forever
            _listenerThread?.Interrupt();
            _listenerThread = null;

            // close all client connections
            foreach (KeyValuePair<int, ClientToken> kvp in _clients) {
                TcpClient client = kvp.Value.Client;
                // close the stream if not closed yet. it may have been closed
                // by a disconnect already, so use try/catch
                try {
                    client.GetStream().Close();
                } catch {
                }

                client.Close();
            }

            // clear clients list
            _clients.Clear();
        }

        // send message to client using socket connection.
        public bool Send(int connectionId, byte[] data) {
            // respect max message size to avoid allocation attacks.
            if (data.Length <= MaxMessageSize) {
                // find the connection
                ClientToken token;
                if (_clients.TryGetValue(connectionId, out token)) {
                    // add to send queue and return immediately.
                    // calling Send here would be blocking (sometimes for long times
                    // if other side lags or wire was disconnected)
                    token.sendQueue.Enqueue(data);
                    token.sendPending.Set(); // interrupt SendThread WaitOne()
                    return true;
                }

                logger.Info("Server.Send: invalid connectionId: " + connectionId);
                return false;
            }

            logger.Error("Client.Send: message too big: " + data.Length + ". Limit: " + MaxMessageSize);
            return false;
        }

        // client's ip is sometimes needed by the server, e.g. for bans
        public string GetClientAddress(int connectionId) {
            // find the connection
            ClientToken token;
            if (_clients.TryGetValue(connectionId, out token)) {
                return ((IPEndPoint) token.Client.Client.RemoteEndPoint).Address.ToString();
            }

            return "";
        }

        /// <summary>
        /// Verify if a client is connected to the server.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool IsClientConnected(int id)
        {
            return _clients.ContainsKey(id);
        }

        // disconnect (kick) a client
        public bool Disconnect(int connectionId) {
            // find the connection
            ClientToken token;
            if (_clients.TryGetValue(connectionId, out token)) {
                // just close it. client thread will take care of the rest.
                token.Client.Close();
                logger.Info("Server.Disconnect connectionId:" + connectionId);
                return true;
            }

            return false;
        }
    }
}