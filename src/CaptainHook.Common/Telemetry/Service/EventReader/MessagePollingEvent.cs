using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry.Service.EventReader
{
    public class MessagePollingEvent : TelemetryEvent
    {
        public string FabricId { get; set; }
        public int MessageCount { get; set; }
        public int ConsecutiveLongPolls { get; set; }
    }
}
