using Microsoft.ServiceFabric.Actors.Runtime;

namespace CaptainHook.Common.Telemetry
{
    public class ActorActivated : ActorTelemetryEvent
    {
        public ActorActivated(ActorBase actor) : base(actor)
        {}
    }
}
