using System;
using System.Collections.Generic;
using System.Text;

namespace CaptainHook.Common.Telemetry
{
    public class ServiceBusDiagnosticEvent
    {
        public string OperationName { get; set; }
        public string Status { get; set; }
        public string Entity { get; set; }
        public string Value { get; set; }
    }
}
