using System;

namespace NetEngineCore.Networking.Exceptions {
    public class OverSizedMessageException : Exception {
        public OverSizedMessageException() {
        }

        public OverSizedMessageException(string message) : base(message) {
        }

        public OverSizedMessageException(string message, Exception inner) : base(message, inner) {
        }
    }
}