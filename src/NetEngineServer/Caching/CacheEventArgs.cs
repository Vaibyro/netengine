using System;

namespace NetEngineServer.Caching {
    public class CacheEventArgs : EventArgs {
        public object Object { get; }
        
        public CacheEventArgs(object obj) {
            Object = obj;
        }
    }
}