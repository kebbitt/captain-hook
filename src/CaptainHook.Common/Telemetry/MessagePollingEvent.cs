using System;
using System.Collections.Generic;
using System.Text;
using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry
{
    public class MessagePollingEvent : TelemetryEvent
    {
        public string FabricId { get; set; }
        public int MessageCount { get; set; }
        public int ConsecutiveLongPolls { get; set; }
    }
}
