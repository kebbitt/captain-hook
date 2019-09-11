using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
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
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Runtime;
using Moq;
using Newtonsoft.Json;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.ReliableCollections;
using ServiceFabric.Mocks.ReplicaSet;
using Xunit;

namespace CaptainHook.Tests.Services.Reliable
{
    public class EventReaderTests
    {
        private readonly StatefulServiceContext _context;

        private readonly IReliableStateManagerReplica2 _stateManager;

        private readonly IBigBrother _mockedBigBrother;

        private readonly ConfigurationSettings _config;

        private readonly MockActorProxyFactory _mockActorProxyFactory;

        private readonly Mock<IMessageReceiver> _mockMessageProvider;

        public EventReaderTests()
        {
            _context = CustomMockStatefulServiceContextFactory.Create(
                Constants.CaptainHookApplication.Services.EventReaderServiceType,
                Constants.CaptainHookApplication.Services.EventReaderServiceFullName,
                Encoding.UTF8.GetBytes("test.type"));

            _mockActorProxyFactory = new MockActorProxyFactory();
            _stateManager = new MockReliableStateManager();
            _config = new ConfigurationSettings();
            _mockedBigBrother = new Mock<IBigBrother>().Object;
            _mockMessageProvider = new Mock<IMessageReceiver>();

        }

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
            _mockActorProxyFactory.RegisterActor(CreateMockEventHandlerActor(new ActorId(handlerName), _mockedBigBrother));

            var count = 0;
            _mockMessageProvider.Setup(s => s.ReceiveAsync(
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
                It.IsAny<string>())).Returns(_mockMessageProvider.Object);

            mockServiceBusProvider.Setup(s => s.GetLockToken(It.IsAny<Message>())).Returns(Guid.NewGuid().ToString);

            var service = new EventReaderService.EventReaderService(
                _context,
                _stateManager,
                _mockedBigBrother,
                mockServiceBusProvider.Object,
                _mockActorProxyFactory,
                _config);

            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await service.InvokeRunAsync(cancellationTokenSource.Token);

            //Assert that the dictionary contains 1 processing message and associated handle
            var dictionary = await _stateManager.GetOrAddAsync<IReliableDictionary2<int, MessageDataHandle>>(nameof(MessageDataHandle));
            Assert.Equal(expectedHandleCount, dictionary.Count);
        }

        [Fact]
        [IsLayer0]
        public async Task CanCancelService()
        {
            _mockMessageProvider.Setup(s => s.ReceiveAsync(
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
                It.IsAny<string>())).Returns(_mockMessageProvider.Object);

            var service = new EventReaderService.EventReaderService(
                _context,
                _stateManager,
                _mockedBigBrother,
                mockServiceBusProvider.Object,
                new MockActorProxyFactory(),
                _config);

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
            //create mocked handlers based on the amount of messages passed in to the test
            var mockActorProxyFactory = new MockActorProxyFactory();
            for (var i = 0; i <= messageCount; i++)
            {
                mockActorProxyFactory.RegisterActor(CreateMockEventHandlerActor(new ActorId($"{eventName}-{i + 1}"), _mockedBigBrother));
            }

            //return messages up to the limit of the test requirements
            var count = 0;
            _mockMessageProvider.Setup(s => s.ReceiveAsync(
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
                It.IsAny<string>())).Returns(_mockMessageProvider.Object);

            mockServiceBusProvider.Setup(s => s.GetLockToken(It.IsAny<Message>())).Returns(Guid.NewGuid().ToString);

            var service = new EventReaderService.EventReaderService(
                _context,
                _stateManager,
                _mockedBigBrother,
                mockServiceBusProvider.Object,
                mockActorProxyFactory,
                _config);

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
            _mockActorProxyFactory.RegisterActor(CreateMockEventHandlerActor(new ActorId(handlerName), _mockedBigBrother));

            var count = 0;
            _mockMessageProvider.Setup(s => s.ReceiveAsync(
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
                It.IsAny<string>())).Returns(_mockMessageProvider.Object);

            mockServiceBusManager.Setup(s => s.GetLockToken(It.IsAny<Message>())).Returns(Guid.NewGuid().ToString);

            var service = new EventReaderService.EventReaderService(
                _context,
                _stateManager,
                _mockedBigBrother,
                mockServiceBusManager.Object,
                _mockActorProxyFactory,
                _config);

            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await service.InvokeRunAsync(cancellationTokenSource.Token);

            var dictionary = await _stateManager.GetOrAddAsync<IReliableDictionary2<int, MessageDataHandle>>(nameof(MessageDataHandle));

            MessageData messageData;
            using (var tx = _stateManager.CreateTransaction())
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
            dictionary = await _stateManager.GetOrAddAsync<IReliableDictionary2<int, MessageDataHandle>>(nameof(MessageDataHandle));
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
            _mockActorProxyFactory.RegisterActor(CreateMockEventHandlerActor(new ActorId(handlerName), _mockedBigBrother));

            var count = 0;
            _mockMessageProvider.Setup(s => s.ReceiveAsync(
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
                It.IsAny<string>())).Returns(_mockMessageProvider.Object);

            mockServiceBusManager.Setup(s => s.GetLockToken(It.IsAny<Message>())).Returns(Guid.NewGuid().ToString);

            EventReaderService.EventReaderService Factory(StatefulServiceContext context, IReliableStateManagerReplica2 stateManager) => 
                new EventReaderService.EventReaderService(
                    _context, 
                    _mockedBigBrother, 
                    mockServiceBusManager.Object, 
                    _mockActorProxyFactory, 
                    _config);

            var replicaSet = new MockStatefulServiceReplicaSet<EventReaderService.EventReaderService>(
                Factory, 
                StateManagerFactory, 
                Constants.CaptainHookApplication.Services.EventReaderServiceType,
                Constants.CaptainHookApplication.Services.EventReaderServiceFullName);

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

        [Fact]
        [IsLayer0]
        public async Task TestPrimaryReplicaShouldHaveOpenListenersAsync()
        {
            MyService Factory(StatefulServiceContext context, IReliableStateManagerReplica2 stateManager) => new MyService(_context, _stateManager);
            var replicaSet = new MockStatefulServiceReplicaSet<MyService>(Factory);
            await replicaSet.AddReplicaAsync(ReplicaRole.Primary, 1);
            var openListeners = replicaSet.Primary.OpenListeners;
            Assert.Equal(1, openListeners.Count());
        }

        private class MyService : StatefulService
        {
            public MyService(StatefulServiceContext serviceContext) : base(serviceContext)
            {
            }

            public MyService(StatefulServiceContext serviceContext, IReliableStateManagerReplica reliableStateManagerReplica) : base(serviceContext, reliableStateManagerReplica)
            {
            }
        }

        private EventReaderService.EventReaderService ServiceFactory(StatefulServiceContext arg1, IReliableStateManagerReplica2 arg2)
        {
            return new EventReaderService.EventReaderService(
                _context,
                _stateManager,
                _mockedBigBrother,
                new Mock<IServiceBusManager>().Object,
                _mockActorProxyFactory,
                _config);
        }

        private IReliableStateManagerReplica2 StateManagerFactory(StatefulServiceContext arg1, TransactedConcurrentDictionary<Uri, IReliableState> arg2)
        {
            return _stateManager;
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