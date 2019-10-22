using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry
{
    public class MessageReceiverNoLongerAvailable: TelemetryEvent
    {
        public string FabricId { get; set; }
    }
}
