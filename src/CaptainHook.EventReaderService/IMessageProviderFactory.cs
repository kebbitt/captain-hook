using System;
using Microsoft.Azure.ServiceBus.Core;

namespace CaptainHook.EventReaderService
{
    /// <summary>
    /// Wrapper for MessageReceiver so it can be mocked for testing
    /// </summary>
    public interface IMessageProviderFactory
    {
        /// <summary>
        /// Configures the MessageReceiver
        /// </summary>
        /// <param name="serviceBusConnectionString"></param>
        /// <param name="topicName"></param>
        /// <param name="subscriptionName"></param>
        MessageReceiver Create(string serviceBusConnectionString, string topicName, string subscriptionName, MessageReceiver previousReceiver= null);

        /// <summary>
        /// Batch size for the receiver to consume from the ServiceBus
        /// </summary>
        int ReceiverBatchSize { get; set; }

        /// <summary>
        /// Minimum backoff time for the ServiceBus retry calls in milliseconds.
        /// </summary>
        TimeSpan BackoffMin { get; set; }

        /// <summary>
        /// Maximum backoff time for the ServiceBus retry calls in milliseconds.
        /// </summary>
        TimeSpan BackoffMax { get; set; }
    }
}
