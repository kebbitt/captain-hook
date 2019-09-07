using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using CaptainHook.Common.Configuration;
using CaptainHook.Common.Telemetry.Service;
using CaptainHook.Interfaces;
using Eshopworld.Core;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Data;
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
        private const string SubscriptionName = "captain-hook";

        private const int BatchSize = 1; // make this dynamic - based on the number of active handlers - more handlers, lower batch

        private readonly IBigBrother _bigBrother;
        private readonly IServiceBusManager _serviceBusManager;
        private readonly IActorProxyFactory _proxyFactory;
        private readonly ConfigurationSettings _settings;
        private readonly string _eventType;

        //todo this should be in the state of the reader, we should be able to deploy and continue from where we were before the deployment
        internal readonly Dictionary<int, string> LockTokens = new Dictionary<int, string>();
        internal readonly Dictionary<int, int> InFlightMessages = new Dictionary<int, int>();

        private IReliableDictionary2<int, MessageDataHandle> _messageHandles;
        private HashSet<int> _freeHandlers = new HashSet<int>();
        private IMessageReceiver _messageReceiver;

        //todo move this to config driven in the code package
        internal int HandlerCount = 10;

        /// <summary>
        /// Default ctor used at runtime
        /// </summary>
        /// <param name="context"></param>
        /// <param name="bigBrother"></param>
        /// <param name="serviceBusManager"></param>
        /// <param name="proxyFactory"></param>
        /// <param name="settings"></param>
        public EventReaderService(
            StatefulServiceContext context,
            IBigBrother bigBrother,
            IServiceBusManager serviceBusManager,
            IActorProxyFactory proxyFactory,
            ConfigurationSettings settings)
            : base(context)
        {
            _bigBrother = bigBrother;
            _serviceBusManager = serviceBusManager;
            _proxyFactory = proxyFactory;
            _settings = settings;
            _eventType = Encoding.UTF8.GetString(context.InitializationData);
        }

        /// <summary>
        /// Ctor used for mocking and tests
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reliableStateManagerReplica"></param>
        /// <param name="bigBrother"></param>
        /// <param name="serviceBusManager"></param>
        /// <param name="proxyFactory"></param>
        /// <param name="settings"></param>
        public EventReaderService(
            StatefulServiceContext context,
            IReliableStateManagerReplica reliableStateManagerReplica,
            IBigBrother bigBrother,
            IServiceBusManager serviceBusManager,
            IActorProxyFactory proxyFactory,
            ConfigurationSettings settings)
            : base(context, reliableStateManagerReplica)
        {
            _bigBrother = bigBrother;
            _serviceBusManager = serviceBusManager;
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

                    LockTokens.Add(handleData.Value.HandlerId, handleData.Value.LockToken);
                    InFlightMessages.Add(handleData.Value.HandlerId, handleData.Value.HandlerId);
                }

                await tx.CommitAsync();

                var maxUsedHandlers = InFlightMessages.Values.OrderByDescending(i => i).FirstOrDefault();
                if (maxUsedHandlers > HandlerCount) HandlerCount = maxUsedHandlers;
                _freeHandlers = Enumerable.Range(1, HandlerCount).Except(InFlightMessages.Values).ToHashSet();
            }
        }

        private async Task SetupServiceBus()
        {
            await _serviceBusManager.CreateAsync(_settings.AzureSubscriptionId, _settings.ServiceBusNamespace, SubscriptionName, _eventType);
            _messageReceiver = _serviceBusManager.CreateMessageReceiver(_settings.ServiceBusConnectionString, _eventType, SubscriptionName);
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            _messageHandles = await StateManager.GetOrAddAsync<IReliableDictionary2<int, MessageDataHandle>>(nameof(MessageDataHandle));

            await BuildInMemoryState(cancellationToken);
            await SetupServiceBus();

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_messageReceiver.IsClosedOrClosing) continue;

                var messages = await _messageReceiver.ReceiveAsync(BatchSize, TimeSpan.FromMilliseconds(50));
                if (messages == null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
                    continue;
                }

                foreach (var message in messages)
                {
                    var messageData = new MessageData(Encoding.UTF8.GetString(message.Body), _eventType);

                    var handlerId = GetFreeHandlerId();

                    messageData.HandlerId = handlerId;
                    messageData.CorrelationId = Guid.NewGuid().ToString();
                    InFlightMessages.Add(messageData.HandlerId, handlerId);
                    LockTokens.Add(handlerId, _serviceBusManager.GetLockToken(message));

                    var handleData = new MessageDataHandle
                    {
                        HandlerId = handlerId,
                        LockToken = _serviceBusManager.GetLockToken(message)
                    };

                    using (var tx = StateManager.CreateTransaction())
                    {
                        await _messageHandles.AddAsync(tx, handleData.HandlerId, handleData);

                        await _proxyFactory.CreateActorProxy<IEventHandlerActor>(
                            new ActorId(messageData.EventHandlerActorId),
                            serviceName: $"{Constants.CaptainHookApplication.Services.EventHandlerServiceShortName}")
                            .Handle(messageData);

                        await tx.CommitAsync();
                    }
                }
            }
        }

        internal int GetFreeHandlerId()
        {
            var handlerId = _freeHandlers.FirstOrDefault();
            if (handlerId == 0)
            {
                return ++HandlerCount;
            }

            _freeHandlers.Remove(handlerId);
            return handlerId;
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
                if (!LockTokens.TryGetValue(messageData.HandlerId, out var lockToken))
                {
                    throw new LockTokenNotFoundException("lock token was not found in the in memory dictionary")
                    {
                        EventType = messageData.Type,
                        HandlerId = messageData.HandlerId,
                        CorrelationId = messageData.CorrelationId,
                        LockTokenKeys = string.Join(",", LockTokens.Select(s=> s.Key).ToArray()),
                        LockTokenValues = string.Join(",", LockTokens.Select(s=> s.Value).ToArray())
                    };
                }

                //let the message naturally expire for redelivery if it is an a successfully delivery.
                if (messageDelivered)
                {
                    await _messageReceiver.CompleteAsync(lockToken);
                }

                LockTokens.Remove(messageData.HandlerId);
                InFlightMessages.Remove(messageData.HandlerId);
                _freeHandlers.Add(messageData.HandlerId);

                using (var tx = StateManager.CreateTransaction())
                {
                    await _messageHandles.TryRemoveAsync(tx, messageData.HandlerId);

                    await tx.CommitAsync();
                }
            }
            catch (Exception e)
            {
                _bigBrother.Publish(e.ToExceptionEvent());
            }
        }
    }
}
