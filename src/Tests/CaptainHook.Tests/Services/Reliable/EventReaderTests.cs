using System;
using System.Collections.Generic;
using System.Fabric;
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
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Runtime;
using Moq;
using Newtonsoft.Json;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.ReplicaSet;
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
        [InlineData("test.type", 3, 10)]
        [InlineData("test.type", 10, 10)]
        [InlineData("test.type", 12, 12)]
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
            var mockServiceBusManager = new Mock<IServiceBusManager>();
            mockServiceBusManager.Setup(s => s.CreateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()));

            mockServiceBusManager.Setup(s => s.CreateMessageReceiver(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(mockMessageProvider.Object);

            mockServiceBusManager.Setup(s => s.GetLockToken(It.IsAny<Message>())).Returns(Guid.NewGuid().ToString);

            var service = new EventReaderService.EventReaderService(
                context,
                stateManager,
                mockedBigBrother.Object,
                mockServiceBusManager.Object,
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

        /// <summary>
        /// Tests the service to determine that it can change role gracefully - while keeping messages and state inflight while migrating to the active secondaries.
        /// </summary>
        /// <returns></returns>
        [Theory]
        [IsLayer0]
        [InlineData("test.type", "test.type-1", 1)]
        [InlineData("test.type", "test.type-1", 10)]
        public async Task PromoteActivateSecondaryToPrimary(string eventName, string handlerName, int messageCount)
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
            var serviceBusManager = new Mock<IServiceBusManager>();
            serviceBusManager.Setup(s => s.CreateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()));

            serviceBusManager.Setup(s => s.CreateMessageReceiver(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(mockMessageProvider.Object);

            serviceBusManager.Setup(s => s.GetLockToken(It.IsAny<Message>())).Returns(Guid.NewGuid().ToString);



            var replicaSet = new MockStatefulServiceReplicaSet<EventReaderService.EventReaderService>(
                CreateEventReaderService(context, stateManager, mockedBigBrother.Object, serviceBusManager.Object, mockActorProxyFactory, config));

            //add a new Primary replica with id 1
            await replicaSet.AddReplicaAsync(ReplicaRole.Primary, 1);
            //add a new ActiveSecondary replica with id 2
            await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 2);
            //add a second ActiveSecondary replica with id 3
            await replicaSet.AddReplicaAsync(ReplicaRole.ActiveSecondary, 3);

            //Run the primary and then stop it with the cancellation token
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await replicaSet.Primary.ServiceInstance.InvokeRunAsync(cancellationTokenSource.Token);


        }

        [Theory]
        [IsLayer0]
        [InlineData("test.type", "test.type-1", 1)]
        [InlineData("test.type", "test.type-1", 10)]
        public async Task DemotePrimaryToActivateSecondary(string eventName, string handlerName, int messageCount)
        {

        }



        private static IList<Message> CreateMessage(string eventName)
        {
            return new List<Message> { new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MessageData("Hello World 1", eventName)))) };
        }

        /// <summary>
        /// Creates an event reader service delegate so that it can be sent to the MockStatefulServiceReplicaSet for role change testing
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reliableStateManagerReplica"></param>
        /// <param name="bigBrother"></param>
        /// <param name="serviceBusManager"></param>
        /// <param name="proxyFactory"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        private static Func<StatefulServiceContext, IReliableStateManagerReplica2, EventReaderService.EventReaderService> CreateEventReaderService(StatefulServiceContext context,
            IReliableStateManagerReplica reliableStateManagerReplica,
            IBigBrother bigBrother,
            IServiceBusManager serviceBusManager,
            IActorProxyFactory proxyFactory,
            ConfigurationSettings config)
        {
            return (c, s) => new EventReaderService.EventReaderService(
                context,
                reliableStateManagerReplica,
                bigBrother,
                serviceBusManager,
                proxyFactory,
                config);
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