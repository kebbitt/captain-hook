using System.Fabric;

namespace CaptainHook.Common.Telemetry.Service
{
    public class ServiceAbortedEvent : ServiceTelemetryEvent
    {
        public ServiceAbortedEvent(StatefulServiceContext context)
            : base(context)
        {

        }
    }
}