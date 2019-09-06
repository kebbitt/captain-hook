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
        private readonly Dictionary<Guid, string> _lockTokens = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, int> _inFlightMessages = new Dictionary<Guid, int>();

        private IReliableDictionary2<Guid, MessageDataHandle> _messageHandles;
        private HashSet<int> _freeHandlers = new HashSet<int>();
        private IMessageReceiver _messageReceiver;

        //todo move this to config driven in the code package
#if DEBUG
        internal int _handlerCount = 1;
#else
        internal int _handlerCount = 10;
#endif
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

                    _lockTokens.Add(handleData.Value.Handle, handleData.Value.LockToken);
                    _inFlightMessages.Add(handleData.Value.Handle, handleData.Value.HandlerId);
                }

                await tx.CommitAsync();

                var maxUsedHandlers = _inFlightMessages.Values.OrderByDescending(i => i).FirstOrDefault();
                if (maxUsedHandlers > _handlerCount) _handlerCount = maxUsedHandlers;
                _freeHandlers = Enumerable.Range(1, _handlerCount).Except(_inFlightMessages.Values).ToHashSet();
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
            _messageHandles = await StateManager.GetOrAddAsync<IReliableDictionary2<Guid, MessageDataHandle>>(nameof(MessageDataHandle));

            await BuildInMemoryState(cancellationToken);
            await SetupServiceBus();

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_messageReceiver.IsClosedOrClosing) continue;

                //todo if no messages for a longer period of time run a longer sleep
                var messages = await _messageReceiver.ReceiveAsync(BatchSize, TimeSpan.FromMilliseconds(50));
                if (messages == null) continue;

                foreach (var message in messages)
                {
                    var messageData = new MessageData(Encoding.UTF8.GetString(message.Body), _eventType);

                    var handlerId = GetFreeHandlerId();

                    messageData.HandlerId = handlerId;
                    messageData.CorrelationId = Guid.NewGuid().ToString();
                    _inFlightMessages.Add(messageData.Handle, handlerId);
                    _lockTokens.Add(messageData.Handle, _serviceBusManager.GetLockToken(message));

                    var handleData = new MessageDataHandle
                    {
                        Handle = messageData.Handle,
                        HandlerId = handlerId,
                        LockToken = _serviceBusManager.GetLockToken(message)
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

        internal int GetFreeHandlerId()
        {
            var handlerId = _freeHandlers.FirstOrDefault();
            if (handlerId == 0)
            {
                return _handlerCount++;
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
                //let the message naturally expire for redelivery if it is an a successfully delivery.
                if (messageDelivered)
                {
                    await _messageReceiver.CompleteAsync(_lockTokens[messageData.Handle]);
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
