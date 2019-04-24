using MessagePack;

namespace NetEngineBench {
    [MessagePackObject()]
    public class EndMessage : Message {
        [Key(0)]
        public string Content { get; set; } = "end";
    }
}