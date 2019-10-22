using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry.Service.EventReader
{
    public class ServiceBusConnectionRecycleEvent : TelemetryEvent
    {
        public string Entity { get; set; }
    }
}
