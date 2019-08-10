using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CaptainHook.Common.Configuration;
using CaptainHook.Common.Telemetry.Actor;
using CaptainHook.Common.Telemetry.Message;
using CaptainHook.Interfaces;
using Eshopworld.Core;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace CaptainHook.EventReaderActor
{
    public class LockedMessage
    {
        public LockedMessage()
        {
            
        }

        public LockedMessage(Message message)
        {
            Message = message;
        }

        /// <summary>
        /// The limit of times the lock of a messages can be renewed. This currently equates to a 5 * 30 seconds limit.
        /// </summary>
        public const int RenewLockLimit = 5;

        /// <summary>
        /// A count of how many times the messages has it's lock renewed
        /// </summary>
        public int LockRenewCount { get; set; }

        /// <summary>
        /// The ServiceBus message
        /// </summary>
        public Message Message { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime LockUntilUtc => Message.SystemProperties.LockedUntilUtc;

        /// <summary>
        /// 
        /// </summary>
        public string LockToken => Message.SystemProperties.LockToken;
    }

    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    public class EventReaderActor : Actor, IEventReaderActor, IRemindable
    {
        private const string SubscriptionName = "captain-hook";

        // TAKE NUMBER OF HANDLERS INTO CONSIDERATION, DO NOT BATCH MORE THEN HANDLERS
        private const int BatchSize = 1; // make this configurable

        private readonly IBigBrother _bigBrother;
        private readonly ConfigurationSettings _settings;
        private readonly object _gate = new object();

        private volatile bool _readingEvents;
        private MessageReceiver _receiver;
        private const string WakeUpReminderName = "Wake up";
        private Dictionary<Guid, LockedMessage> _activeMessages;

        /// <summary>
        /// Initializes a new instance of EventReaderActor
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        /// <param name="bigBrother">The <see cref="IBigBrother"/> telemetry instance that this actor instance will use to publish.</param>
        /// <param name="settings">The <see cref="ConfigurationSettings"/> being read from the KeyVault.</param>
        public EventReaderActor(ActorService actorService, ActorId actorId, IBigBrother bigBrother, ConfigurationSettings settings)
            : base(actorService, actorId)
        {
            _bigBrother = bigBrother;
            _settings = settings;
        }

        protected override async Task OnActivateAsync()
        {
            try
            {
                _bigBrother.Publish(new ActorActivated(this));

                await InitialiseState();

                await SetupServiceBusAsync();

                _receiver = new MessageReceiver(
                    _settings.ServiceBusConnectionString,
                    EntityNameHelper.FormatSubscriptionPath(TypeExtensions.GetEntityName(Id.GetStringId()),
                        SubscriptionName),
                    ReceiveMode.PeekLock,
                    new RetryExponential(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), 3),
                    BatchSize);

                ////registers a timer for the actor to poll the queue
                RegisterTimer(RenewLockAsync,
                    null,
                    TimeSpan.FromMilliseconds(5000),
                    TimeSpan.FromMilliseconds(5000));

            }
            catch (Exception e)
            {
                _bigBrother.Publish(e.ToExceptionEvent());
                throw;
            }

            await base.OnActivateAsync();
        }

        private async Task InitialiseState()
        {
            var activeMessages = await StateManager.TryGetStateAsync<Dictionary<Guid, LockedMessage>>(nameof(_activeMessages));
            if (activeMessages.HasValue)
            {
                _activeMessages = activeMessages.Value;
            }
            else
            {
                _activeMessages = new Dictionary<Guid, LockedMessage>();
                await StateManager.AddOrUpdateStateAsync(nameof(_activeMessages), _activeMessages, (s, value) => value);
            }
        }

        protected override Task OnDeactivateAsync()
        {
            _bigBrother.Publish(new ActorDeactivated(this));
            return base.OnDeactivateAsync();
        }

        internal async Task SetupServiceBusAsync()
        {
            var token = new AzureServiceTokenProvider().GetAccessTokenAsync("https://management.core.windows.net/", string.Empty).Result;
            var tokenCredentials = new TokenCredentials(token);

            var client = RestClient.Configure()
                                   .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
                                   .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                                   .WithCredentials(new AzureCredentials(tokenCredentials, tokenCredentials, string.Empty, AzureEnvironment.AzureGlobalCloud))
                                   .Build();

            var sbNamespace = Azure.Authenticate(client, string.Empty)
                                   .WithSubscription(_settings.AzureSubscriptionId)
                                   .ServiceBusNamespaces.List()
                                   .SingleOrDefault(n => n.Name == _settings.ServiceBusNamespace);

            if (sbNamespace == null)
            {
                throw new InvalidOperationException($"Couldn't find the service bus namespace {_settings.ServiceBusNamespace} in the subscription with ID {_settings.AzureSubscriptionId}");
            }

            var azureTopic = await sbNamespace.CreateTopicIfNotExists(TypeExtensions.GetEntityName(Id.GetStringId()));
            await azureTopic.CreateSubscriptionIfNotExists(SubscriptionName);
        }

        //todo this should not pool the service bus if the handler is full.
        internal async Task ReadEventsAsync(object _)
        {
            try
            {
                lock (_gate)
                {
                    if (_readingEvents) return;
                    _readingEvents = true;
                }

                if (_receiver.IsClosedOrClosing) return;

                var messages = await _receiver.ReceiveAsync(BatchSize, TimeSpan.FromMilliseconds(50));
                if (messages == null) return;

                foreach (var message in messages)
                {
                    //get a message, sends it to the pool manager and then sends it to an event handler, comes back then with a handle.
                    var handle = await ActorProxy.Create<IPoolManagerActor>(new ActorId(0)).DoWork(Encoding.UTF8.GetString(message.Body), Id.GetStringId());

                    _activeMessages.Add(handle, new LockedMessage(message));
                    await StateManager.AddOrUpdateStateAsync(nameof(_activeMessages), _activeMessages, (s, value) => value);
                }
            }
            catch (Exception e)
            {
                _bigBrother.Publish(e.ToExceptionEvent());
                throw;
            }
            finally
            {
                _readingEvents = false;
            }
        }

        /// <summary>
        /// Renews the message lock for each messages which is active
        /// todo think about putting this into it's own actor per message
        /// </summary>
        /// <param name="_"></param>
        /// <returns></returns>
        internal async Task RenewLockAsync(object _)
        {
            //todo think about a finite limit of renews for a message
            foreach (var activeMessage in _activeMessages.Keys)
            {
                var message = _activeMessages[activeMessage];

                if (message.LockUntilUtc >= DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(5)))
                {
                    if (_activeMessages[activeMessage].LockRenewCount >= LockedMessage.RenewLockLimit)
                    {
                        _bigBrother.Publish(new MessageRenewLimitEvent
                        {
                            EventName = Id.GetStringId(),
                            HandleId = activeMessage.ToString(),
                            Count = _activeMessages[activeMessage].LockRenewCount
                        });
                        continue;
                    }

                    await _receiver.RenewLockAsync(message.LockToken);
                    _activeMessages[activeMessage].LockRenewCount++;
                    _bigBrother.Publish(new MessageRenewEvent
                    {
                        EventName = Id.GetStringId(),
                        HandleId = activeMessage.ToString(),
                        Count = _activeMessages[activeMessage].LockRenewCount
                    });
                }
            }
        }

        /// <remarks>
        /// Do nothing by design. We just need to make sure that the actor is properly activated.
        /// </remarks>>
        public async Task Run()
        {
            await this.RegisterReminderAsync(
                WakeUpReminderName,
                null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));
        }

        public async Task CompleteMessage(Guid handle, bool messageSuccess)
        {
            //todo NOT HANDLING FAULTS YET - BE CAREFUL HERE!
            try
            {
                if (_activeMessages.ContainsKey(handle))
                {
                    _activeMessages.Remove(handle);
                    await StateManager.AddOrUpdateStateAsync(nameof(_activeMessages), _activeMessages, (s, value) => value);
                }

                if (messageSuccess)
                {
                    await _receiver.CompleteAsync(_activeMessages[handle].LockToken);
                }
            }
            catch (Exception e)
            {
                _bigBrother.Publish(e.ToExceptionEvent());
                throw;
            }
        }

        public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            if (reminderName.Equals(WakeUpReminderName, StringComparison.OrdinalIgnoreCase))
            {
                await ReadEventsAsync(null);
            }
        }
    }
}
