using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace CaptainHook.Common.Telemetry
{
    public abstract class ActorTelemetryEvent : TelemetryEvent
    {
        public string ActorName { get; set; }

        public string ActorId { get; set; }

        public ActorTelemetryEvent(ActorBase actor)

        {
            ActorId = actor.Id.ToString();
            ActorName = actor.ActorService.ActorTypeInformation.ServiceName;
        }
    }
}