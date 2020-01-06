using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace CaptainHook.EventReaderService
{
    public class MessageProviderFactory : IMessageProviderFactory
    {
        private ServiceBusConnection conn;
        public IMessageReceiver Create(string serviceBusConnectionString, string topicName, string subscriptionName, bool dlqMode)
        {
            Validate(subscriptionName);
            Validate(serviceBusConnectionString);
            Validate(topicName);

            if (conn == null)
            {
                conn = new ServiceBusConnection(new ServiceBusConnectionStringBuilder(serviceBusConnectionString));
            }

            var path = EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName);

            if (dlqMode)
            {
                path += "/$deadletterqueue";
            }

            return new MessageReceiver(
            conn,
            path,
            ReceiveMode.PeekLock,
            new RetryExponential(
                BackoffMin,
                BackoffMax,
                ReceiverRetryCount),
            ReceiverBatchSize);
        }

        /// <summary>
        /// Batch size for the receiver to consume from the ServiceBus. Defaults to 10
        /// </summary>
        public int ReceiverBatchSize { get; set; } = 0; //do not use pre-fetch mode

        /// <summary>
        /// Minimum backoff time for the ServiceBus retry calls in milliseconds. Defaults to 100ms
        /// </summary>
        public TimeSpan BackoffMin { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Maximum backoff time for the ServiceBus retry calls in milliseconds. Defaults to 500ms
        /// </summary>
        public TimeSpan BackoffMax { get; set; } = TimeSpan.FromMilliseconds(500);

        public const int ReceiverRetryCount = 3;

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
            if (prop == default)
            {
                throw new ArgumentException("value cannot be zero", nameof(prop));
            }
        }
    }
}
