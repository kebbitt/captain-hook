using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace CaptainHook.EventReaderService
{
    public class MessageProviderFactory : IMessageProviderFactory
    {
        public IMessageReceiver Builder(string serviceBusConnectionString, string topicName, string subscription)
        {
            Validate(subscription);
            Validate(serviceBusConnectionString);
            Validate(topicName);

            MessageReceiver = new MessageReceiver(
                serviceBusConnectionString,
                EntityNameHelper.FormatSubscriptionPath(topicName, subscription),
                ReceiveMode.PeekLock,
                new RetryExponential(TimeSpan.FromMilliseconds(BackoffMin), TimeSpan.FromMilliseconds(BackoffMax), ReceiverBatchSize),
                ReceiverBatchSize);

            return MessageReceiver;
        }

        /// <summary>
        /// Batch size for the receiver to consume from the ServiceBus. Defaults to 3
        /// </summary>
        public int ReceiverBatchSize { get; set; } = 3;

        /// <summary>
        /// Minimum backoff time for the ServiceBus retry calls in milliseconds. Defaults to 100ms
        /// </summary>
        public int BackoffMin { get; set; } = 100;

        /// <summary>
        /// Maximum backoff time for the ServiceBus retry calls in milliseconds. Defaults to 500ms
        /// </summary>
        public int BackoffMax { get; set; } = 500;

        public IMessageReceiver MessageReceiver { get; private set; }

        private void Validate(string prop)
        {
            if (string.IsNullOrWhiteSpace(prop))
            {
                throw new ArgumentNullException(nameof(prop));
            }
        }

        private void Validate(int prop)
        {
            if(prop == default(int))
            {
                throw new ArgumentException("value cannot be zero", nameof(prop));
            }
        }
    }
}