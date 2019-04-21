using System;

namespace NetEngineCore.Networking.Exceptions {
    public class LostConnectionException : Exception {
        public LostConnectionException() {
        }

        public LostConnectionException(string message) : base(message) {
        }

        public LostConnectionException(string message, Exception inner) : base(message, inner) {
        }
    }
}