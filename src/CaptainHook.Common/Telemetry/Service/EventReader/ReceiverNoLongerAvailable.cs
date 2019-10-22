using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry.Service.EventReader
{
    public class MessageReceiverNoLongerAvailable : TelemetryEvent
    {
        public string FabricId { get; set; }
    }
}
