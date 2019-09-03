using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using CaptainHook.Common.Configuration;
using CaptainHook.EventHandlerActor.Handlers;
using CaptainHook.EventReaderService;
using CaptainHook.Interfaces;
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data.Collections;
using Moq;
using Newtonsoft.Json;
using ServiceFabric.Mocks;
using Xunit;

namespace CaptainHook.Tests.Services.Actors
{
    public class EventReaderTests
    {
        /// <summary>
        /// Tests the reader can get a message from ServiceBus and create a handler to process it.
        /// Expectation is that state in the reader will contain handlers in the reliable dictionary
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="handlerName"></param>
        /// <param name="expectedHandleCount"></param>
        /// <returns></returns>
        [Theory]
        [IsLayer0]
        [InlineData("test.type", "test.type-1", 1)]
        public async Task CanGetMessage(string eventName, string handlerName, int expectedHandleCount)
        {
            var context = CustomMockStatefulServiceContextFactory.Create(
                Constants.CaptainHookApplication.Services.EventReaderServiceType,
                Constants.CaptainHookApplication.Services.EventReaderServiceFullName,
                Encoding.UTF8.GetBytes(eventName));

            var stateManager = new MockReliableStateManager();
            var mockedBigBrother = new Mock<IBigBrother>();
            var config = new ConfigurationSettings();

            var mockActorProxyFactory = new MockActorProxyFactory();
            mockActorProxyFactory.RegisterActor(CreateMockEventHandlerActor(new ActorId(handlerName), mockedBigBrother.Object));

            var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MessageData("Hello World", eventName))));

            var mockMessageProvider = new Mock<IMessageReceiver>();
            mockMessageProvider.Setup(s => s.ReceiveAsync(
                It.IsAny<int>(),
                It.IsAny<TimeSpan>())).ReturnsAsync(new List<Message> { message });

            var mockServiceBusProvider = new Mock<IServiceBusProvider>();
            mockServiceBusProvider.Setup(s => s.CreateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()));

            mockServiceBusProvider.Setup(s => s.CreateMessageReceiver(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(mockMessageProvider.Object);

            mockServiceBusProvider.Setup(s => s.GetLockToken(It.IsAny<Message>())).Returns(Guid.NewGuid().ToString);

            var service = new EventReaderService.EventReaderService(
                context,
                stateManager,
                mockedBigBrother.Object,
                mockServiceBusProvider.Object,
                mockActorProxyFactory,
                config);

            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await service.InvokeRunAsync(cancellationTokenSource.Token);

            //Assert that the dictionary contains 1 processing message and associated handle
            var dictionary = await stateManager.GetOrAddAsync<IReliableDictionary2<Guid, MessageDataHandle>>(nameof(MessageDataHandle));
            Assert.Equal(expectedHandleCount, dictionary.Count);
        }

        [Theory]
        [IsLayer0]
        [InlineData("test.type", "test.type-1", 1)]
        public async Task CanCancel(string eventName, string handlerName, int expectedHandleCount)
        {
            var context = CustomMockStatefulServiceContextFactory.Create(
                Constants.CaptainHookApplication.Services.EventReaderServiceType,
                Constants.CaptainHookApplication.Services.EventReaderServiceFullName,
                Encoding.UTF8.GetBytes(eventName));

            var stateManager = new MockReliableStateManager();
            var mockedBigBrother = new Mock<IBigBrother>();
            var config = new ConfigurationSettings();

            var mockActorProxyFactory = new MockActorProxyFactory();
            mockActorProxyFactory.RegisterActor(CreateMockEventHandlerActor(new ActorId(handlerName), mockedBigBrother.Object));

            var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MessageData("Hello World", eventName))));

            var mockMessageProvider = new Mock<IMessageReceiver>();
            mockMessageProvider.Setup(s => s.ReceiveAsync(
                It.IsAny<int>(),
                It.IsAny<TimeSpan>())).ReturnsAsync(new List<Message> { message });

            var mockServiceBusProvider = new Mock<IServiceBusProvider>();
            mockServiceBusProvider.Setup(s => s.CreateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()));

            mockServiceBusProvider.Setup(s => s.CreateMessageReceiver(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(mockMessageProvider.Object);

            mockServiceBusProvider.Setup(s => s.GetLockToken(It.IsAny<Message>())).Returns(Guid.NewGuid().ToString);

            var service = new EventReaderService.EventReaderService(
                context,
                stateManager,
                mockedBigBrother.Object,
                mockServiceBusProvider.Object,
                mockActorProxyFactory,
                config);

            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await service.InvokeRunAsync(cancellationTokenSource.Token);

            //Assert can cancel the service from running
            Assert.True(cancellationTokenSource.IsCancellationRequested);
        }

        public async Task ReaderDoesntGetMessagesWhenHandlersAreFull()
        {

        }

        public async Task CanDeleteMessageFromSubscription()
        {

        }

        public async Task CheckHandlesInInMemoryState()
        {

        }

        private static IEventHandlerActor CreateMockEventHandlerActor(ActorId id, IBigBrother bb)
        {
            ActorBase ActorFactory(ActorService service, ActorId actorId) => new MockEventHandlerActor(service, id);
            var svc = MockActorServiceFactory.CreateActorServiceForActor<MockEventHandlerActor>(ActorFactory);
            var actor = svc.Activate(id);
            return actor;
        }

        private class MockEventHandlerActor : Actor, IEventHandlerActor
        {
            public MockEventHandlerActor(ActorService actorService, ActorId actorId) : base(actorService, actorId)
            {
            }

            public Task Handle(MessageData messageData)
            {
                return Task.CompletedTask;
            }
        }
    }

    public class DirectorServiceTests
    {
        public async Task CanCreateReaders()
        {

        }

        public async Task CanCreateHandler()
        {

        }
    }

    public class EventHandlerActorTests
    {
        [Fact]
        [IsLayer0]
        public async Task CheckHasTimerAfterHandleCall()
        {
            var bigBrotherMock = new Mock<IBigBrother>().Object;

            var eventHandlerActor = CreateEventHandlerActor(new ActorId(1), bigBrotherMock);

            await eventHandlerActor.Handle(new MessageData(string.Empty, "test.type"));

            var timers = eventHandlerActor.GetActorTimers();
            Assert.True(timers.Any());
        }

        //todo
        [Theory]
        [IsLayer0]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CallReaderToCompleteMessage(bool expectedMessageDelivered)
        {

        }

        private static EventHandlerActor.EventHandlerActor CreateEventHandlerActor(ActorId id, IBigBrother bigBrother)
        {
            ActorBase ActorFactory(ActorService service, ActorId actorId) => new EventHandlerActor.EventHandlerActor(service, id, new Mock<IEventHandlerFactory>().Object, bigBrother);
            var svc = MockActorServiceFactory.CreateActorServiceForActor<EventHandlerActor.EventHandlerActor>(ActorFactory);
            var actor = svc.Activate(id);
            return actor;
        }
    }
}