using System;
using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry.Service.EventReader
{
    public class ServiceBusDiagnosticEvent : TelemetryEvent
    {
        public string OperationName { get; set; }
        public string Status { get; set; }
        public string Entity { get; set; }
        public string Value { get; set; }
        public double Duration { get; set; }
        public long ReplicaId { get; set; }
        public string PollGuid { get; set; }
        public DateTime PollProcessTime { get; set; }
    }
}
