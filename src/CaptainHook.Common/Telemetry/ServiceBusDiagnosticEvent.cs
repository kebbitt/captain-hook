using System;
using System.Collections.Generic;
using System.Text;
using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry
{
    public class ServiceBusDiagnosticEvent : TelemetryEvent
    {
        public string OperationName { get; set; }
        public string Status { get; set; }
        public string Entity { get; set; }
        public string Value { get; set; }
        public double Duration { get; set; }        
        public long ReplicaId { get; set; }
    }
}
