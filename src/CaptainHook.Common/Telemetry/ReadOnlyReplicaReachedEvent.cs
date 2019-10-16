using System;
using System.Collections.Generic;
using System.Text;
using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry
{
    public class ReadOnlyReplicaReachedEvent : TelemetryEvent
    {
        public string Id { get; set; }
        public long ReplicaId { get; set; }

        public string WriteStatus { get; set; }
    }
}
