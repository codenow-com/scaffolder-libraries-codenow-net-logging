namespace CodeNow.Logging.Structs
{
    public class LogStruct
    {
        public string Context { get; set; } = "default";
        public string Level { get; set; }
        public string Logger { get; set; }
        public MdcStruct Mdc { get; set; }
        public string Message { get; set; }
        public string TimeStamp { get; set; }
    }
}