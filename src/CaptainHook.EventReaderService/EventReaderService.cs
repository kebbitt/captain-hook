using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using CaptainHook.Common.Configuration;
using CaptainHook.Common.ServiceModels;
using CaptainHook.Common.Telemetry.Service;
using CaptainHook.Common.Telemetry.Service.EventReader;
using CaptainHook.Interfaces;
using Eshopworld.Core;
using Eshopworld.Telemetry;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;

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
        private string _eventType;
        private string _dispatchName; 

        internal ConcurrentDictionary<string, MessageDataHandle> _inflightMessages = new ConcurrentDictionary<string, MessageDataHandle>();

        private ConcurrentQueue<int> _freeHandlers = new ConcurrentQueue<int>();
        private CancellationToken _cancellationToken;
        private IDisposable _diagSourceOuterSub;
        private IDisposable _diagSourceInnerSub;

        //todo move this to config driven in the code package
        internal int HandlerCount = 10;

        ///defines behavior of new connection - any poll above <see cref="LongPollThreshold"/> will add to the <see cref="MessageReceiverWrapper.ConsecutiveLongPollCount"/> 
        ///and if <see cref="consecutiveLongPollThreshold"/> is reached, new active receiver is created
        private readonly TimeSpan LongPollThreshold = TimeSpan.FromMilliseconds(5000);
        private readonly int ConsecutiveLongPollThreshold = 10;
        //force time out timespan for phased out receivers
        private readonly TimeSpan ForcedReceiverCloseTimeout = TimeSpan.FromMinutes(2);

        internal ConcurrentDictionary<Guid, MessageReceiverWrapper> _messageReceivers = new ConcurrentDictionary<Guid, MessageReceiverWrapper>();
        internal MessageReceiverWrapper _activeMessageReader;



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
            ParseOutInitData(context.InitializationData);
        }

        private void ParseOutInitData(byte[] initializationData)
        {
            if (initializationData == null || initializationData.Length == 0)
                throw new ArgumentException("invalid initialization data structure", nameof(initializationData));

            var json = JsonSerializer.CreateDefault().Deserialize<EventReaderInitData>(new JsonTextReader(new StringReader(Encoding.UTF8.GetString(initializationData))));

            if (json == null)
                throw new ArgumentException("failed to deserialize init data", nameof(initializationData));

            _eventType = json.EventType;
            _dispatchName = json.DispatchName;
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
            ParseOutInitData(context.InitializationData);
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

        protected override async Task OnOpenAsync(ReplicaOpenMode openMode, CancellationToken cancellationToken)
        {
            _bigBrother.Publish(new ServiceActivatedEvent(Context, InFlightMessageCount));
            await base.OnOpenAsync(openMode, cancellationToken);
        }

        protected override async Task OnCloseAsync(CancellationToken cancellationToken)
        {
            _bigBrother.Publish(new ServiceDeactivatedEvent(Context, InFlightMessageCount));
            await base.OnCloseAsync(cancellationToken);
        }

        protected override void OnAbort()
        {
            _bigBrother.Publish(new ServiceAbortedEvent(Context, InFlightMessageCount));
            base.OnAbort();
        }

        protected override async Task OnChangeRoleAsync(ReplicaRole newRole, CancellationToken cancellationToken)
        {
            _bigBrother.Publish(new ServiceRoleChangeEvent(Context, newRole, InFlightMessageCount));
            await base.OnChangeRoleAsync(newRole, cancellationToken);
        }

        /// <summary>
        /// Determines the number of handlers to have based on the number of messages which are inflight
        /// </summary>
        /// <returns></returns>
        internal async Task GetHandlerCountsOnStartup()
        {
            _freeHandlers = Enumerable.Range(1, HandlerCount).ToConcurrentQueue();
        }

        /// <summary>
        /// Gets the count of the numbers of messages which are in flight at the moment
        /// </summary>
        internal int InFlightMessageCount => HandlerCount - _freeHandlers.Count;

        private async Task SetupServiceBus()
        {
            ServicePointManager.DefaultConnectionLimit = 100;

            await _serviceBusManager.CreateAsync(_settings.AzureSubscriptionId, _settings.ServiceBusNamespace, SubscriptionName, _eventType);
            
            var messageReceiver = _serviceBusManager.CreateMessageReceiver(_settings.ServiceBusConnectionString, _eventType, SubscriptionName);

            //add new receiver and set is as primary
            var wrapper = new MessageReceiverWrapper { Receiver = messageReceiver, ReceiverId = Guid.NewGuid() };
            _activeMessageReader = wrapper;

            _messageReceivers.TryAdd(wrapper.ReceiverId, wrapper); //this will always succeed (key is a new guid)

            if (_diagSourceInnerSub == null && _diagSourceOuterSub == null)
            {
                //set up diagnostic source
                _diagSourceOuterSub = DiagnosticListener.AllListeners.Subscribe(delegate (DiagnosticListener listener)
                {
                    // subscribe to the Service Bus DiagnosticSource
                    if (listener.Name == "Microsoft.Azure.ServiceBus")
                    {
                        // receive event from Service Bus DiagnosticSource
                        _diagSourceInnerSub = listener.Subscribe(delegate (KeyValuePair<string, object> evnt)
                            {
                            // Log operation details once it's done
                            if (evnt.Key.EndsWith("Stop"))
                                {
                                    Activity currentActivity = Activity.Current;
                                    var opName = currentActivity.OperationName;
                                    TaskStatus status = (TaskStatus)evnt.Value.GetProperty("Status");
                                    var entity = (string)evnt.Value.GetProperty("Entity");

                                    if (opName.EndsWith("Exception", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var excp = (Exception)evnt.Value.GetProperty("Exception");
                                        _bigBrother.Publish(excp.ToExceptionEvent());
                                    }

                                    _bigBrother.Publish(new ServiceBusDiagnosticEvent
                                    {
                                        OperationName = opName,
                                        Status = status.ToString(),
                                        Value = evnt.Value.ToString(),
                                        Entity = entity,
                                        Duration = currentActivity.Duration.TotalMilliseconds,
                                        ReplicaId = Context.ReplicaId,
                                        PollGuid = Guid.NewGuid().ToString(),
                                        PollProcessTime = DateTime.UtcNow
                                    });
                                }
                            });
                    }
                });
            }
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;

            try
            {
                await GetHandlerCountsOnStartup();
                await SetupServiceBus();

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        //if (_messageReceiver.IsClosedOrClosing)
                        //{
                        //    _bigBrother.Publish(new MessageReceiverClosingEvent { FabricId = $"{this.Context.ServiceName}:{this.Context.ReplicaId}" });

                        //    continue;
                        //}

                        var (messages, activeReaderId) = await ReceiveMessagesFromActiveReceiver();

                        await ServiceReceiversLifecycle();

                        if (messages == null || messages.Count == 0)
                        {
                            // ReSharper disable once MethodSupportsCancellation - no need to cancellation token here
#if DEBUG
                            //this is done due to enable SF mocks to run as receiver call is not awaited and therefore RunAsync would never await
                            await Task.Delay(TimeSpan.FromMilliseconds(10));
#endif
                            continue;
                        }

                        foreach (var message in messages)
                        {
                            var messageData = new MessageData(Encoding.UTF8.GetString(message.Body), _eventType);

                            var handlerId = GetFreeHandlerId();

                            messageData.HandlerId = handlerId;
                            messageData.CorrelationId = Guid.NewGuid().ToString();

                            var handleData = new MessageDataHandle
                            {
                                LockToken = _serviceBusManager.GetLockToken(message),
                                ReceiverId = activeReaderId
                            };

                            _inflightMessages.TryAdd(messageData.CorrelationId, handleData); //should again always succeed

                            await _proxyFactory.CreateActorProxy<IEventHandlerActor>(
                                    new ActorId(messageData.EventHandlerActorId),
                                    serviceName: Constants.CaptainHookApplication.Services.EventHandlerServiceShortName)
                                .Handle(messageData);
                        }
                    }
                    catch (ServiceBusCommunicationException sbCommunicationException)
                    {
                        BigBrother.Write(sbCommunicationException.ToExceptionEvent());
                        await SetupServiceBus();
                    }
                    catch (Exception e)
                    {
                        BigBrother.Write(e.ToExceptionEvent());
                    }
                }

                _bigBrother.Publish(new Common.Telemetry.CancellationRequestedEvent { FabricId = $"{this.Context.ServiceName}:{this.Context.ReplicaId}" });

                //shutdown all kinds of things
                if (_diagSourceInnerSub != null)
                {
                    _diagSourceInnerSub.Dispose();
                }
                if (_diagSourceOuterSub != null)
                {
                    _diagSourceOuterSub.Dispose();
                }
            }
            catch (Exception e)
            {
                BigBrother.Write(e.ToExceptionEvent());
            }
        }

        /// <summary>
        /// //TODO: add doco
        /// </summary>
        /// <returns></returns>
        private async Task ServiceReceiversLifecycle()
        {
            //detect new connection needed
            if (_activeMessageReader.ConsecutiveLongPollCount >= ConsecutiveLongPollThreshold)
            {
                await ResetConnection();
            }
            //close exhausted phased out receivers
            var list = _messageReceivers.Where(r => r.Value != _activeMessageReader && (r.Value.ReceivedCount == 0 || DateTime.Now >= r.Value.ForceClosureAt)).ToList();

            foreach (var item in list)
            {
                //await item.Value.Receiver.CloseAsync(); 
                _messageReceivers.Remove(item.Key, out _);
            }
        }

        private async Task<(IList<Message> messages, Guid activeReader)> ReceiveMessagesFromActiveReceiver()
        {
            var receiveSW = new Stopwatch();
            receiveSW.Start();

            var messages = await _activeMessageReader.Receiver.ReceiveAsync(BatchSize, LongPollThreshold);

            receiveSW.Stop();

            if (messages != null && messages.Count != 0)
            {
                Interlocked.Add(ref _activeMessageReader.ReceivedCount, messages.Count);
            }

            if (receiveSW.ElapsedMilliseconds > LongPollThreshold.TotalMilliseconds)
                _activeMessageReader.ConsecutiveLongPollCount++;
            else
                _activeMessageReader.ConsecutiveLongPollCount = 0;

            _bigBrother.Publish(new MessagePollingEvent { FabricId = $"{Context.ServiceName}:{Context.ReplicaId}", MessageCount = messages != null ? messages.Count : 0, ConsecutiveLongPolls = _activeMessageReader.ConsecutiveLongPollCount });

            return (messages, _activeMessageReader.ReceiverId);
        }

        private async Task ResetConnection()
        {
            _bigBrother.Publish(new ServiceBusConnectionRecycleEvent { Entity = Context.ServiceName.ToString() });

            _activeMessageReader.ForceClosureAt = DateTime.Now.Add(ForcedReceiverCloseTimeout);

            await SetupServiceBus();
        }

        internal int GetFreeHandlerId()
        {
            if (_freeHandlers.TryDequeue(out var handlerId))
            {
                return handlerId;
            }

            return ++HandlerCount;
        }

        /// <summary>
        /// Completes the messages if the delivery was successful, else message is not removed from service bus and allowed to be redelivered
        /// </summary>
        /// <param name="messageData"></param>
        /// <param name="messageDelivered"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task CompleteMessageAsync(MessageData messageData, bool messageDelivered, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_inflightMessages.TryRemove(messageData.CorrelationId, out var handle))
                {
                    throw new LockTokenNotFoundException("lock token was not found in inflight message queue")
                    {
                        EventType = messageData.Type,
                        HandlerId = messageData.HandlerId,
                        CorrelationId = messageData.CorrelationId
                    };
                }

                try
                {
                    //let the message naturally expire if it's an unsuccessful delivery
                    if (messageDelivered)
                    {
                        //try to lookup the receiver 
                        if (_messageReceivers.TryGetValue(handle.ReceiverId, out var receiverWrapper))
                        {
                            Interlocked.Decrement(ref receiverWrapper.ReceivedCount);
                            await receiverWrapper.Receiver.CompleteAsync(handle.LockToken);
                        }
                        else
                        {
                            _bigBrother.Publish(new MessageReceiverNoLongerAvailable { FabricId = Context.ServiceName.ToString() });
                        }
                    }
                }
                catch (MessageLockLostException e)
                {
                    BigBrother.Write(e.ToExceptionEvent());
                }
            }
            catch (Exception e)
            {
                BigBrother.Write(e.ToExceptionEvent());
            }
            finally
            {
                _freeHandlers.Enqueue(messageData.HandlerId);
            }
        }
    }

    public static class PropertyExtensions
    {
        public static object GetProperty(this object _this, string propertyName)
        {
            return _this.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(_this);
        }
    };

    internal class MessageReceiverWrapper
    {
        internal IMessageReceiver Receiver;
        internal int ReceivedCount;
        internal DateTime ForceClosureAt;
        internal Guid ReceiverId;
        internal int ConsecutiveLongPollCount;
    }
}
