namespace CodeNow.Logging.Structs
{
    public class MdcStruct
    {
        public string SpanId { get; set; }
        public string ParentSpanId { get; set; }
        public string TraceId { get; set; }
    }
}