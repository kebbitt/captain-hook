using Microsoft.ServiceFabric.Actors.Runtime;

namespace CaptainHook.Common.Telemetry.Actor
{
    public class ActorError : ActorTelemetryEvent
    {
        public ActorError(string message, ActorBase actor) : base(actor)
        {
            Message = message;
        }

        public string Message { get; set; }
    }
}