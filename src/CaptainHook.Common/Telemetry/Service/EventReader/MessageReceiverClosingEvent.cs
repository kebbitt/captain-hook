using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry.Service.EventReader
{
    public class MessageReceiverClosingEvent : TelemetryEvent
    {
        public string FabricId { get; set; }
    }
}
