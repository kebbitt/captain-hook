using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry.Message
{
    public class UnknownMessageType : TelemetryEvent
    {
        public string Type { get; set; }

        public string Payload { get; set; }
    }
}