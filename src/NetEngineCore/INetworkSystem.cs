using System;
using NetEngineCore.Messaging.Dispatching;

namespace NetEngineCore {
    public interface INetworkSystem {
        void Run();
        void Stop();
        event EventHandler Ready;
        event EventHandler Starting;
        event EventHandler Stopped;
        event EventHandler Stopping;
        bool Running { get; }
        int Port { get; }
        bool UseSsl { get; set; }
        string CertificateFile { get; set; }
        IMessageDispatcher Dispatcher { get; }
    }
}