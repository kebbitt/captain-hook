using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using CaptainHook.Common.Configuration;
using CaptainHook.Interfaces;
using Eshopworld.Core;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace CaptainHook.EventReaderService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// Reads the messages from the subscription.
    /// Keeps an list of in flight messages and tokens which can be used for renewal as well as deleting the message from the subscription when it's complete
    /// </summary>
    public class EventReaderService : StatefulService, IEventReaderService
    {
        internal const string SubscriptionName = "captain-hook";

        private const int BatchSize = 1; // make this dynamic - based on the number of active handlers - more handlers, lower batch

        private readonly IBigBrother _bigBrother;
        private readonly IMessageProviderFactory _messageProviderFactoryFactory;
        private readonly IActorProxyFactory _proxyFactory;
        private readonly ConfigurationSettings _settings;
        private readonly string _eventType;

        //todo this should be in the state of the reader, we should be able to deploy and continue from where we were before the deployment
        private readonly Dictionary<Guid, string> _lockTokens = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, int> _inFlightMessages = new Dictionary<Guid, int>();

        private IReliableDictionary2<Guid, MessageDataHandle> _messageHandles;
        private HashSet<int> _freeHandlers = new HashSet<int>();
        private IMessageReceiver _receiver;

#if DEBUG
        internal int HandlerCount = 1;
#else
        internal int HandlerCount = 10;
#endif

        public EventReaderService(
            StatefulServiceContext context,
            IBigBrother bigBrother,
            IMessageProviderFactory messageProviderFactoryFactory,
            IActorProxyFactory proxyFactory,
            ConfigurationSettings settings)
            : base(context)
        {
            _bigBrother = bigBrother;
            _messageProviderFactoryFactory = messageProviderFactoryFactory;
            _proxyFactory = proxyFactory;
            _settings = settings;
            _eventType = Encoding.UTF8.GetString(context.InitializationData);
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }

        internal async Task SetupServiceBus()
        {
            var azureTopic = await ServiceBusNamespaceExtensions.SetupTopic(
                _settings.AzureSubscriptionId,
                _settings.ServiceBusNamespace,
                TypeExtensions.GetEntityName(_eventType));

            await azureTopic.CreateSubscriptionIfNotExists(SubscriptionName);

            _receiver = _messageProviderFactoryFactory.Builder(
                _settings.ServiceBusConnectionString,
                TypeExtensions.GetEntityName(_eventType),
                SubscriptionName);
        }

        internal async Task BuildInMemoryState(CancellationToken cancellationToken)
        {
            using (var tx = StateManager.CreateTransaction())
            {
                var enumerator = (await _messageHandles.CreateKeyEnumerableAsync(tx)).GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var handleData = await _messageHandles.TryGetValueAsync(tx, enumerator.Current);
                    if (!handleData.HasValue) continue;

                    _lockTokens.Add(handleData.Value.Handle, handleData.Value.LockToken);
                    _inFlightMessages.Add(handleData.Value.Handle, handleData.Value.HandlerId);
                }

                await tx.CommitAsync();

                var maxUsedHandlers = _inFlightMessages.Values.OrderByDescending(i => i).FirstOrDefault();
                if (maxUsedHandlers > HandlerCount) HandlerCount = maxUsedHandlers;
                _freeHandlers = Enumerable.Range(1, HandlerCount).Except(_inFlightMessages.Values).ToHashSet();
            }
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            _messageHandles = await StateManager.GetOrAddAsync<IReliableDictionary2<Guid, MessageDataHandle>>(nameof(MessageDataHandle));

            await BuildInMemoryState(cancellationToken);
            await SetupServiceBus();

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_receiver.IsClosedOrClosing) continue;

                //todo harden this up - don't take any more messages unless we have free handles
                if (_freeHandlers.Count == 0) continue;

                var messages = await _receiver.ReceiveAsync(BatchSize, TimeSpan.FromMilliseconds(50));
                if (messages == null) continue;

                foreach (var message in messages)
                {
                    var messageData = new MessageData(Encoding.UTF8.GetString(message.Body), _eventType);

                    var handlerId = _freeHandlers.FirstOrDefault();
                    if (handlerId == 0)
                    {
                        handlerId = ++HandlerCount;
                    }
                    else
                    {
                        _freeHandlers.Remove(handlerId);
                    }

                    messageData.HandlerId = handlerId;
                    messageData.CorrelationId = Guid.NewGuid().ToString();
                    _inFlightMessages.Add(messageData.Handle, handlerId);
                    _lockTokens.Add(messageData.Handle, message.SystemProperties.LockToken);

                    var handleData = new MessageDataHandle
                    {
                        Handle = messageData.Handle,
                        HandlerId = handlerId,
                        LockToken = message.SystemProperties.LockToken
                    };

                    using (var tx = StateManager.CreateTransaction())
                    {
                        await _messageHandles.AddAsync(tx, handleData.Handle, handleData);

                        await _proxyFactory.CreateActorProxy<IEventHandlerActor>(
                            new ActorId(messageData.EventHandlerActorId),
                            serviceName: $"{Constants.CaptainHookApplication.Services.EventHandlerServiceShortName}")
                            .Handle(messageData);

                        await tx.CommitAsync();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageData"></param>
        /// <param name="messageDelivered"></param>
        /// <returns></returns>
        public async Task CompleteMessage(MessageData messageData, bool messageDelivered)
        {
            try
            {
                //let the message naturally expire for redelivery if it is an a successfully delivery.
                if (messageDelivered)
                {
                    await _receiver.CompleteAsync(_lockTokens[messageData.Handle]);
                }

                _lockTokens.Remove(messageData.Handle);
                _inFlightMessages.Remove(messageData.Handle);
                _freeHandlers.Add(messageData.HandlerId);

                using (var tx = StateManager.CreateTransaction())
                {
                    await _messageHandles.TryRemoveAsync(tx, messageData.Handle);

                    await tx.CommitAsync();
                }
            }
            catch (Exception e)
            {
                _bigBrother.Publish(e.ToExceptionEvent());
                throw;
            }
        }
    }
}
