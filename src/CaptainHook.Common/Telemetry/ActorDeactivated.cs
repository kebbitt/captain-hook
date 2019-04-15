using Microsoft.ServiceFabric.Actors.Runtime;

namespace CaptainHook.Common.Telemetry
{
    public class ActorDeactivated : ActorTelemetryEvent
    {
        public ActorDeactivated(ActorBase actor) : base(actor)
        {}
    }
}
