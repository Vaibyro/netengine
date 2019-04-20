// incoming message queue of <connectionId, message>
// (not a HashSet because one connection can have multiple new messages)
// -> a class, so that we don't copy the whole struct each time

namespace NetEngineCore.Networking {
    public class Packet {
        public int ConnectionId { get; }
        public PacketType PacketType { get; }
        public byte[] Data { get; }

        public Packet(int connectionId, PacketType packetType, byte[] data) {
            ConnectionId = connectionId;
            PacketType = packetType;
            Data = data;
        }
    }
}