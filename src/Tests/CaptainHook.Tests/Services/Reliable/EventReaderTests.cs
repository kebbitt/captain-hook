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
        /// <param name="messageCount">limit to simulate the number of messages in the queue in the mock</param>
        /// <returns></returns>
        [Theory]
        [IsLayer0]
        [InlineData("test.type", "test.type-1", 1, 1)]
        public async Task CanGetMessage(string eventName, string handlerName, int expectedHandleCount, int messageCount)
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

            var mockMessageProvider = new Mock<IMessageReceiver>();

            var count = 0;
            mockMessageProvider.Setup(s => s.ReceiveAsync(
                It.IsAny<int>(),
                It.IsAny<TimeSpan>())).ReturnsAsync(() =>
            {
                if (count >= messageCount)
                {
                    return new List<Message>();
                }
                count++;
                return CreateMessage(eventName);
            });
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
            var dictionary = await stateManager.GetOrAddAsync<IReliableDictionary2<int, MessageDataHandle>>(nameof(MessageDataHandle));
            Assert.Equal(expectedHandleCount, dictionary.Count);
        }

        [Theory]
        [IsLayer0]
        [InlineData("test.type")]
        public async Task CanCancelService(string eventName)
        {
            var context = CustomMockStatefulServiceContextFactory.Create(
                Constants.CaptainHookApplication.Services.EventReaderServiceType,
                Constants.CaptainHookApplication.Services.EventReaderServiceFullName,
                Encoding.UTF8.GetBytes(eventName));

            var stateManager = new MockReliableStateManager();
            var mockedBigBrother = new Mock<IBigBrother>();
            var config = new ConfigurationSettings();

            var mockMessageProvider = new Mock<IMessageReceiver>();
            mockMessageProvider.Setup(s => s.ReceiveAsync(
                It.IsAny<int>(),
                It.IsAny<TimeSpan>())).ReturnsAsync(new List<Message>());

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

            var service = new EventReaderService.EventReaderService(
                context,
                stateManager,
                mockedBigBrother.Object,
                mockServiceBusProvider.Object,
                new MockActorProxyFactory(),
                config);

            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await service.InvokeRunAsync(cancellationTokenSource.Token);

            //Assert can cancel the service from running
            Assert.True(cancellationTokenSource.IsCancellationRequested);
        }

        [Theory]
        [IsLayer0]
        [InlineData("test.type", 3, 3)]
        [InlineData("test.type", 10, 10)]
        public async Task CanAddMoreHandlersDynamically(string eventName, int messageCount, int expectedHandlerId)
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
                mockActorProxyFactory.RegisterActor(CreateMockEventHandlerActor(new ActorId($"{eventName}-{i + 1}"), mockedBigBrother.Object));
            }

            var mockMessageProvider = new Mock<IMessageReceiver>();

            //return messages up to the limit of the test requirements
            var count = 0;
            mockMessageProvider.Setup(s => s.ReceiveAsync(
                It.IsAny<int>(),
                It.IsAny<TimeSpan>())).ReturnsAsync(() =>
            {
                if (count >= messageCount)
                {
                    return new List<Message>();
                }
                count++;
                return CreateMessage(eventName);
            });

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

            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await service.InvokeRunAsync(cancellationTokenSource.Token);

            Assert.Equal(expectedHandlerId, service.HandlerCount);
        }

        [Theory]
        [IsLayer0]
        [InlineData("test.type", "test.type-1", 1, 1, true, 0)]
        [InlineData("test.type", "test.type-1", 1, 1, false, 0)]
        public async Task CanDeleteMessageFromSubscription(
            string eventName,
            string handlerName,
            int messageCount,
            int expectedHandlerId,
            bool messageDelivered,
            int expectedStatMessageCount)
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

            var mockMessageProvider = new Mock<IMessageReceiver>();

            var count = 0;
            mockMessageProvider.Setup(s => s.ReceiveAsync(
                It.IsAny<int>(),
                It.IsAny<TimeSpan>())).ReturnsAsync(() =>
                {
                    if (count >= messageCount)
                    {
                        return new List<Message>();
                    }
                    count++;
                    return CreateMessage(eventName);
                });
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

            var dictionary = await stateManager.GetOrAddAsync<IReliableDictionary2<int, MessageDataHandle>>(nameof(MessageDataHandle));

            MessageData messageData;
            using (var tx = stateManager.CreateTransaction())
            {
                var messageDataHandle = await dictionary.TryGetValueAsync(tx, expectedHandlerId);
                //reconstruct the message so we can call complete
                messageData = new MessageData("Hello World 1", eventName)
                {
                    HandlerId = messageDataHandle.Value.HandlerId
                };
            }

            await service.CompleteMessage(messageData, messageDelivered);

            //Assert that the dictionary contains 1 processing message and associated handle
            dictionary = await stateManager.GetOrAddAsync<IReliableDictionary2<int, MessageDataHandle>>(nameof(MessageDataHandle));
            Assert.Equal(expectedStatMessageCount, dictionary.Count);
        }
        
        private static IList<Message> CreateMessage(string eventName)
        {
            return new List<Message> { new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MessageData("Hello World 1", eventName)))) };
        }

        private static IEventHandlerActor CreateMockEventHandlerActor(ActorId id, IBigBrother bb)
        {
            ActorBase ActorFactory(ActorService service, ActorId actorId) => new MockEventHandlerActor(service, id);
            var svc = MockActorServiceFactory.CreateActorServiceForActor<MockEventHandlerActor>(ActorFactory);
            var actor = svc.Activate(id);
            return actor;
        }

        /// <summary>
        /// A mock of the handler for the ActorFactory which can be called
        /// </summary>
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