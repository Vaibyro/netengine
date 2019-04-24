using System;
using NetEngineCore.Messaging.Dispatching;

namespace NetEngineCore {
    public interface INetworkSystem {
        bool Running { get; }
        int Port { get; }
        bool UseSsl { get; set; }
        string CertificateFile { get; set; }
        PacketProcessingMode PacketProcessingMode { get; set; }
        IMessageDispatcher Dispatcher { get; }
        event EventHandler Ready;
        event EventHandler Starting;
        event EventHandler Stopped;
        event EventHandler Stopping;
        void Run();
        void Stop();
    }
}