using System.Fabric;

namespace CaptainHook.Common.Telemetry.Service
{
    public class ServiceDeactivatedEvent : ServiceTelemetryEvent
    {
        public ServiceDeactivatedEvent(StatefulServiceContext context)
            : base(context)
        {
            
        }
    }
}