using System;
using System.Collections.Generic;
using System.Text;
using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry
{
    public class MessageReceiverClosingEvent : TelemetryEvent
    {
        public string FabricId { get; set; }
    }
}
