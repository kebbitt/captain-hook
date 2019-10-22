using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace CaptainHook.EventReaderService
{
    /// <inheritdoc/>
    public class ServiceBusManager : IServiceBusManager
    {
        private readonly IMessageProviderFactory _factory;

        public ServiceBusManager(IMessageProviderFactory factory)
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

        public MessageReceiver CreateMessageReceiver(string serviceBusConnectionString, string topicName, string subscriptionName, MessageReceiver previousReceiver=null)
        {
            return _factory.Create(serviceBusConnectionString, TypeExtensions.GetEntityName(topicName), subscriptionName, previousReceiver);
        }

        public string GetLockToken(Message message)
        {
            return message.SystemProperties.LockToken;
        }
    }
}
