using System;
using System.Collections.Generic;
using System.Text;
using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry
{
    public class ServiceBusConnectionRecycleEvent : TelemetryEvent
    {
        public string Entity { get; set; }
    }
}
