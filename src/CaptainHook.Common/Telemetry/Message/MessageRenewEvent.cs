using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry.Message
{
    public class MessageRenewEvent : TelemetryEvent
    {
        public string EventName { get; set; }

        public string HandleId { get; set; }

        public int Count { get; set; }
    }
}