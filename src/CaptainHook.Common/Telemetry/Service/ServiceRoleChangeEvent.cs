using System.Fabric;

namespace CaptainHook.Common.Telemetry.Service
{
    public class ServiceRoleChangeEvent : ServiceTelemetryEvent
    {
        public ServiceRoleChangeEvent(StatefulServiceContext context, ReplicaRole role) : base(context)
        {
            ReplicaRole = role.ToString();
        }

        public string ReplicaRole { get; set; }
    }
}