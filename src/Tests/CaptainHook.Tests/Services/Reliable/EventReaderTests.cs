using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using CaptainHook.Common.Configuration;
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

namespace CaptainHook.Tests.Services.Reliable
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

            var mockServiceBusProvider = new Mock<IServiceBusManager>();
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

        [Theory(Skip = "Skip")]
        [IsLayer0]
        [InlineData("test.type", "test.type-1")]
        public async Task CanCancel(string eventName, string handlerName)
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

            var mockServiceBusProvider = new Mock<IServiceBusManager>();
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

        [Theory(Skip = "Skip")]
        [IsLayer0]
        [InlineData("test.type", 3, 3)]
        [InlineData("test.type", 10, 10)]
        public async Task ReaderAddsMoreHandlersIfHandlersAreFull(string eventName, int messageCount, int expectedHandlerId)
        {
            var context = CustomMockStatefulServiceContextFactory.Create(
                Constants.CaptainHookApplication.Services.EventReaderServiceType,
                Constants.CaptainHookApplication.Services.EventReaderServiceFullName,
                Encoding.UTF8.GetBytes(eventName));

            var stateManager = new MockReliableStateManager();
            var mockedBigBrother = new Mock<IBigBrother>();
            var config = new ConfigurationSettings();

            //create mocked handlers based on the amount of messages passed in to the test
            var mockActorProxyFactory = new MockActorProxyFactory();
            for (var i = 0; i <= messageCount; i++)
            {
                mockActorProxyFactory.RegisterActor(CreateMockEventHandlerActor(new ActorId($"{eventName}-{i+1}"), mockedBigBrother.Object));
            }

            var list = CreateMessages(messageCount, eventName);

            var mockMessageProvider = new Mock<IMessageReceiver>();
            mockMessageProvider.Setup(s => s.ReceiveAsync(
                It.IsAny<int>(),
                It.IsAny<TimeSpan>())).ReturnsAsync(list);

            var mockServiceBusProvider = new Mock<IServiceBusManager>();
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

            Assert.Equal(expectedHandlerId, service._handlerCount);
        }


        public async Task CanDeleteMessageFromSubscription()
        {

        }

        private static IList<Message> CreateMessages(int messageCount, string eventName)
        {
            var list = new List<Message>(messageCount);
            for (var i = 0; i < messageCount; i++)
            {
                list.Add(new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MessageData("Hello World 1", eventName)))));
            }

            return list;
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
}