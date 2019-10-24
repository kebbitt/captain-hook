using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace CaptainHook.EventReaderService
{
    public class MessageProviderFactory : IMessageProviderFactory
    {
        private ServiceBusConnection conn;
        public IMessageReceiver Create(string serviceBusConnectionString, string topicName, string subscriptionName)
        {
            Validate(subscriptionName);
            Validate(serviceBusConnectionString);
            Validate(topicName);

            if (conn==null)
            {
                conn = new ServiceBusConnection(new ServiceBusConnectionStringBuilder(serviceBusConnectionString));
            }

                return new MessageReceiver(
                conn,
                EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName),
                ReceiveMode.PeekLock,
                new RetryExponential(
                    BackoffMin,
                    BackoffMax,
                    ReceiverBatchSize),
                ReceiverBatchSize);
        }

        /// <summary>
        /// Batch size for the receiver to consume from the ServiceBus. Defaults to 10
        /// </summary>
        public int ReceiverBatchSize { get; set; } = 10;

        /// <summary>
        /// Minimum backoff time for the ServiceBus retry calls in milliseconds. Defaults to 100ms
        /// </summary>
        public TimeSpan BackoffMin { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Maximum backoff time for the ServiceBus retry calls in milliseconds. Defaults to 500ms
        /// </summary>
        public TimeSpan BackoffMax { get; set; } = TimeSpan.FromMilliseconds(500);

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
