using Microsoft.ServiceFabric.Actors.Runtime;

namespace CaptainHook.Common.Telemetry.Actor
{
    public class ActorDeactivated : ActorTelemetryEvent
    {
        public ActorDeactivated(ActorBase actor) : base(actor)
        {}
    }
}
