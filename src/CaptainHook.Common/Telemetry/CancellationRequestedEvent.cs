using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry
{
    public class CancellationRequestedEvent : TelemetryEvent
    {
        public string FabricId { get; set; }

        public int InflightMessageCount { get; set; }
    }
}
