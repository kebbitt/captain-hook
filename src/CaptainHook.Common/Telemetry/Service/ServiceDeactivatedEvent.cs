using System.Fabric;

namespace CaptainHook.Common.Telemetry.Service
{
    public class ServiceDeactivatedEvent : ServiceTelemetryEvent
    {
        public ServiceDeactivatedEvent(StatefulServiceContext context, int inFlightMessageCount)
            : base(context)
        {
            InFlightMessageCount = inFlightMessageCount;
        }

        public int InFlightMessageCount { get; set; }
    }
}