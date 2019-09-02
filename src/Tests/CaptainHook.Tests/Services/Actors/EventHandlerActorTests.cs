using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Numerics;
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
using Moq;
using Newtonsoft.Json;
using ServiceFabric.Mocks;
using Xunit;

namespace CaptainHook.Tests.Services.Actors
{
    public class MockStatefulServiceContextFactory
    {
        public const string ServiceTypeName = "MockServiceType";
        public const string ServiceName = "fabric:/MockApp/MockStatefulService";

        public static StatefulServiceContext Default { get; } = ServiceFabric.Mocks.MockStatefulServiceContextFactory.Create(MockCodePackageActivationContext.Default, "MockServiceType", new Uri("fabric:/MockApp/MockStatefulService"), Guid.NewGuid(), long.MaxValue);

        public static StatefulServiceContext Create(
            ICodePackageActivationContext codePackageActivationContext,
            string serviceTypeName,
            Uri serviceName,
            Guid partitionId,
            long replicaId)
        {
            return new StatefulServiceContext(new NodeContext("Node0", new NodeId((BigInteger)0, (BigInteger)1), (BigInteger)0, "NodeType1", "MOCK.MACHINE"), codePackageActivationContext, serviceTypeName, serviceName, (byte[])null, partitionId, replicaId);
        }
    }

    public class MockStatefulServiceContextFactory
    {
        public StatefulServiceContext Create()
    }


    public class EventReaderTests
    {
        [Theory]
        [IsLayer0]
        [InlineData("test.type", "test.type-1")]
        public async Task CanGetMessages(string eventName, string handlerName)
        {
            var context = MockStatefulServiceContextFactory.Create(
                MockCodePackageActivationContext.Default, 
                Constants.CaptainHookApplication.Services.EventReaderServiceType,
                new Uri($"fabric:/{Constants.CaptainHookApplication.ApplicationName}/{Constants.CaptainHookApplication.Services.EventReaderServiceName}.{eventName}"), 
                Guid.NewGuid(), 
                long.MaxValue);

            var mockedBigBrother = new Mock<IBigBrother>();
            var config = new ConfigurationSettings();

            var message = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MessageData("Hello World", eventName)));

            var mockMessageProvider = new Mock<IMessageReceiver>();
            mockMessageProvider.Setup(s => s.ReceiveAsync(
                It.IsAny<int>(),
                It.IsAny<TimeSpan>())).ReturnsAsync(new List<Message> { new Message(message) });

            var mockActorProxyFactory = new MockActorProxyFactory();
            mockActorProxyFactory.RegisterActor(CreateMockEventHandlerActor(new ActorId(handlerName), mockedBigBrother.Object));

            var mockMessageProviderFactory = new Mock<IMessageProviderFactory>();
            mockMessageProviderFactory.Setup(s => s.Builder(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(mockMessageProvider.Object);

            var service = new EventReaderService.EventReaderService(
                context,
                mockedBigBrother.Object,
                mockMessageProviderFactory.Object,
                mockActorProxyFactory,
                config);

            await service.InvokeRunAsync(CancellationToken.None);

            //mock the service bus provider
        }

        [Fact]
        [IsLayer0]
        public async Task CanCancel()
        {

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
                throw new NotImplementedException();
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