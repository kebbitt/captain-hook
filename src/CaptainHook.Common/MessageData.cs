using System;

namespace CaptainHook.Common
{
    public class MessageData
    {
        public Guid Handle { get; set; }

        public string Payload { get; set; }

        public string Type { get; set; }

        /// <summary>
        /// Temp means to wire flows together until end to end actor telemetry tracking is complete
        /// Leave this as a handle for now, will go to being standing correlation id when full flow is wired up.
        /// </summary>
        public string CorrelationId => Handle.ToString();
    }
}
