using System.Fabric;

namespace CaptainHook.Common.Telemetry.Service
{
    public class ServiceRoleChangeEvent : ServiceTelemetryEvent
    {
        public ServiceRoleChangeEvent(StatefulServiceContext context, ReplicaRole role, int inFlightMessageCount) : base(context)
        {
            ReplicaRole = role.ToString();
            InFlightMessageCount = inFlightMessageCount;
        }

        public string ReplicaRole { get; set; }

        public int InFlightMessageCount { get; set; }
    }
}