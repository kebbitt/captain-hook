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
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace CaptainHook.EventReaderService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    public class EventReaderService : StatefulService, IEventReaderService
    {
        internal const string SubscriptionName = "captain-hook";

        // TAKE NUMBER OF HANDLERS INTO CONSIDERATION, DO NOT BATCH MORE THEN HANDLERS
        private const int BatchSize = 1; // make this dynamic - based on the number of active handlers - more handlers, lower batch

        internal readonly IBigBrother Bb;
        internal readonly ConfigurationSettings Settings;
        internal readonly string EventType;

        internal readonly Dictionary<Guid, string> LockTokens = new Dictionary<Guid, string>();
        internal readonly Dictionary<Guid, int> InFlightMessages = new Dictionary<Guid, int>();

        internal IReliableDictionary2<Guid, MessageDataHandle> MessageHandles;
        internal HashSet<int> FreeHandlers = new HashSet<int>();
        internal MessageReceiver Receiver;

#if DEBUG
        internal int HandlerCount = 1;
#else
        internal int HandlerCount = 10;
#endif

        public EventReaderService(StatefulServiceContext context, IBigBrother bb, ConfigurationSettings settings)
            : base(context)
        {
            Bb = bb;
            Settings = settings;
            EventType = Encoding.UTF8.GetString(context.InitializationData);
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
                Settings.AzureSubscriptionId,
                Settings.ServiceBusNamespace,
                TypeExtensions.GetEntityName(EventType));

            await azureTopic.CreateSubscriptionIfNotExists(SubscriptionName);
        }

        internal async Task BuildInMemoryState(CancellationToken cancellationToken)
        {
            using (var tx = StateManager.CreateTransaction())
            {
                var enumerator = (await MessageHandles.CreateKeyEnumerableAsync(tx)).GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var handleData = await MessageHandles.TryGetValueAsync(tx, enumerator.Current);
                    if (!handleData.HasValue) continue;

                    LockTokens.Add(handleData.Value.Handle, handleData.Value.LockToken);
                    InFlightMessages.Add(handleData.Value.Handle, handleData.Value.HandlerId);
                }

                await tx.CommitAsync();

                var maxUsedHandlers = InFlightMessages.Values.OrderByDescending(i => i).FirstOrDefault();
                if (maxUsedHandlers > HandlerCount) HandlerCount = maxUsedHandlers;
                FreeHandlers = Enumerable.Range(1, HandlerCount).Except(InFlightMessages.Values).ToHashSet();
            }
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            MessageHandles = await StateManager.GetOrAddAsync<IReliableDictionary2<Guid, MessageDataHandle>>(nameof(MessageDataHandle));

            await BuildInMemoryState(cancellationToken);
            await SetupServiceBus();

            Receiver = new MessageReceiver(
                Settings.ServiceBusConnectionString,
                EntityNameHelper.FormatSubscriptionPath(TypeExtensions.GetEntityName(EventType), SubscriptionName),
                ReceiveMode.PeekLock,
                new RetryExponential(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), 3),
                BatchSize);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Receiver.IsClosedOrClosing) continue; // TODO: put a circuit breaker here - also add recycling of the receiver to improve reliability

                var messages = await Receiver.ReceiveAsync(BatchSize, TimeSpan.FromMilliseconds(50));
                if (messages == null) continue;

                foreach (var message in messages)
                {
                    var messageData = new MessageData(Encoding.UTF8.GetString(message.Body), EventType);

                    var handlerId = FreeHandlers.FirstOrDefault();
                    if (handlerId == 0)
                    {
                        handlerId = ++HandlerCount;
                    }
                    else
                    {
                        FreeHandlers.Remove(handlerId);
                    }

                    messageData.HandlerId = handlerId;
                    InFlightMessages.Add(messageData.Handle, handlerId);
                    LockTokens.Add(messageData.Handle, message.SystemProperties.LockToken);

                    var handleData = new MessageDataHandle
                    {
                        Handle = messageData.Handle,
                        HandlerId = handlerId,
                        LockToken = message.SystemProperties.LockToken
                    };


                    using (var tx = StateManager.CreateTransaction())
                    {
                        await MessageHandles.AddAsync(tx, handleData.Handle, handleData);

                        // TODO: This doesn't work as-is anymore, since there are multiple service instances for the handler actor service
                        // TODO: Shouldn't attempt to rebuild the service instance name, but instead serialize a JSon initializer payload with eventtype + handler instance name
                        //await ActorProxy.Create<IEventHandlerActor>(new ActorId(messageData.EventHandlerActorId)).HandleMessage(messageData);

                        await tx.CommitAsync();
                    }
                }
            }
        }

        public Task CompleteMessage(MessageData messageData)
        {
            return Task.CompletedTask;
        }
    }
}
