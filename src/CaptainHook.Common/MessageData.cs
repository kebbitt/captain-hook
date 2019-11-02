namespace CaptainHook.Common
{
    public class MessageData
    {
        // ReSharper disable once UnusedMember.Local - Use by the data contract serializers
        private MessageData() { }

        public MessageData(string payload, string type, string subscriberName)
        {
            Payload = payload;
            Type = type;
            SubscriberName = subscriberName;
        }

        /// <summary>
        /// Temp means to wire flows together until end to end actor telemetry tracking is complete
        /// </summary>
        public string CorrelationId { get; set; }
        
        public int HandlerId { get; set; }

        public string Payload { get; set; }

        /// <summary>
        /// The full name of the type of the serialized payload.
        /// </summary>
        public string Type { get; set; }      

        /// <summary>
        /// The optional name of the webhook which should handle the message.
        /// </summary>
        /// <remarks>
        /// If this property is not set the first handler should handle the message
        /// (for backward compatibility).
        /// The webhook must be top level (not a callback).
        /// </remarks>
        public string SubscriberName { get; set; }

        public string EventHandlerActorId => $"{Type}-{HandlerId}";

    }
}
