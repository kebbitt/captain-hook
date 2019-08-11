using System;

namespace CaptainHook.Common
{
    public class MessageData
    {
        // ReSharper disable once UnusedMember.Local - Use by the data contract serializers
        private MessageData() { }

        public MessageData(string payload, string type)
        {
            Handle = Guid.NewGuid();
            Payload = payload;
            Type = type;
        }

        /// <summary>
        /// Temp means to wire flows together until end to end actor telemetry tracking is complete
        /// </summary>
        public string CorrelationId { get; set; }

        public Guid Handle { get; }

        public int HandlerId { get; set; }

        public string Payload { get; }

        public string Type { get; }

        public string EventHandlerActorId => $"{Type}-{HandlerId}";

    }
}
