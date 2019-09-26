using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Features.Indexed;
using CaptainHook.Common;
using CaptainHook.Common.Configuration;
using CaptainHook.Common.Telemetry.Actor;
using CaptainHook.EventHandlerActor.Handlers;
using CaptainHook.Interfaces;
using Eshopworld.Core;
using Eshopworld.Telemetry;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace CaptainHook.EventHandlerActor
{
    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    public class EventHandlerActor : Actor, IEventHandlerActor
    {
        private readonly IBigBrother _bigBrother;
        private readonly IIndex<string, EventHandlerConfig> _eventHandlerConfig;
        private IActorTimer _handleTimer;

        /// <summary>
        /// Initializes a new instance of EventHandlerActor
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        /// <param name="bigBrother"></param>
        public EventHandlerActor(
            ActorService actorService,
            ActorId actorId,
            IBigBrother bigBrother,
            IIndex<string, EventHandlerConfig> eventHandlerConfig)
            : base(actorService, actorId)
        {
            _bigBrother = bigBrother;
            _eventHandlerConfig = eventHandlerConfig;
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override async Task OnActivateAsync()
        {
            _bigBrother.Publish(new ActorActivated(this));

            var names = (await StateManager.GetStateNamesAsync()).Take(1).ToList();
            if (names.Any())
            {
                _handleTimer = RegisterTimer(
                    InternalHandle,
                    names.FirstOrDefault(),
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.MaxValue);
            }
        }

        protected override Task OnDeactivateAsync()
        {
            _bigBrother.Publish(new ActorDeactivated(this));
            return base.OnDeactivateAsync();
        }

        public async Task Handle(MessageData messageData)
        {
            await StateManager.AddOrUpdateStateAsync(messageData.HandlerId.ToString(), messageData, (s, pair) => pair);

            _handleTimer = RegisterTimer(
                InternalHandle,
                messageData,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.MaxValue);
        }

        private async Task InternalHandle(object state)
        {
            var messageDelivered = true;
            MessageData messageData = null;
            try
            {
                UnregisterTimer(_handleTimer);

                messageData = state as MessageData;
                if (messageData == null)
                {
                    _bigBrother.Publish(new ActorError($" actor timer state could not be parsed to a guid so removing it.", this));
                    return;
                }

                if (string.IsNullOrWhiteSpace(messageData.Type))
                {
                    _bigBrother.Publish(new ActorError($"message is missing type - it cannot be processed", this));
                    return;
                }

                await SendToDispatcher(messageData);
                //var handler = _eventHandlerFactory.CreateEventHandler(messageData.Type);
                //await handler.CallAsync(messageData, new Dictionary<string, object>(), CancellationToken.None);
            }
            catch (Exception e)
            {
                BigBrother.Write(e.ToExceptionEvent());
                messageDelivered = false;
            }
            finally
            {
                if (messageData != null)
                {
                    try
                    {

                        await StateManager.RemoveStateAsync(messageData.HandlerId.ToString());

                        var readerServiceNameUri =
                            $"fabric:/{Constants.CaptainHookApplication.ApplicationName}/{Constants.CaptainHookApplication.Services.EventReaderServiceShortName}.{messageData.Type}";
                        await ServiceProxy.Create<IEventReaderService>(new Uri(readerServiceNameUri))
                            .CompleteMessage(messageData, messageDelivered);
                    }
                    catch (Exception e)
                    {
                        BigBrother.Write(e.ToExceptionEvent());
                    }
                }
            }
        }

        private Task SendToDispatcher(MessageData messageData)
        {
            //look up config
            if (!_eventHandlerConfig.TryGetValue(messageData.Type.ToLower(), out var eventHandlerConfig))
            {
                throw new Exception($"Boom, handler event type {messageData.Type} was not found, cannot process the message");
            }
        }
    }
}
