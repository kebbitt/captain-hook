using Microsoft.ServiceFabric.Actors.Runtime;

namespace CaptainHook.Common.Telemetry
{
    public class PoolManagerActorTelemetryEvent : ActorTelemetryEvent
    {
        public PoolManagerActorTelemetryEvent(string msg, ActorBase actor) : base(actor)
        {

        }

        public string Message { get; set; }

        public int FreeHandlerCount { get; set; }

        public int BusyHandlerCount { get; set; }
    }
}