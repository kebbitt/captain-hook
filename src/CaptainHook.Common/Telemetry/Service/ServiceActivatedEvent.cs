using System.Fabric;

namespace CaptainHook.Common.Telemetry.Service
{
    public class ServiceActivatedEvent : ServiceTelemetryEvent
    {
        public ServiceActivatedEvent(StatefulServiceContext context) 
            : base(context)
        {
        }
    }
}