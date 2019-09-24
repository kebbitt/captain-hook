using System.Fabric;

namespace CaptainHook.Common.Telemetry.Service
{
    public class ServiceActivatedEvent : ServiceTelemetryEvent
    {
        public ServiceActivatedEvent(StatefulServiceContext context, int inFlightMessageCount) 
            : base(context)
        {
            InFlightMessageCount = inFlightMessageCount;
        }

        public int InFlightMessageCount { get; set; }
    }
}