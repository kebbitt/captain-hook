using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace CaptainHook.EventReaderService
{
    /// <inheritdoc/>
    public class ServiceBusProvider : IServiceBusProvider
    {
        private readonly IMessageProviderFactory _factory;

        public ServiceBusProvider(IMessageProviderFactory factory)
        {
            _factory = factory;
        }

        public async Task CreateAsync(string azureSubscriptionId, string serviceBusNamespace, string subscriptionName, string topicName)
        {
            var azureTopic = await ServiceBusNamespaceExtensions.SetupTopic(
                azureSubscriptionId,
                serviceBusNamespace,
                TypeExtensions.GetEntityName(topicName));

            await azureTopic.CreateSubscriptionIfNotExists(subscriptionName);
        }

        public IMessageReceiver CreateMessageReceiver(string serviceBusConnectionString, string topicName, string subscriptionName)
        {
            return _factory.Create(serviceBusConnectionString, topicName, subscriptionName);
        }

        public string GetLockToken(Message message)
        {
            return message.SystemProperties.LockToken;
        }
    }
}