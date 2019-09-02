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
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;
using Moq;
using ServiceFabric.Mocks;
using Xunit;

namespace CaptainHook.Tests.Services.Actors
{
    public class EventReaderTests
    {
        [Fact]
        [IsLayer0]
        public async Task CanGetMessages()
        {
            var context = MockStatefulServiceContextFactory.Default;
            var actorFactory = new ActorProxyFactory();
            var mockedBigBrother = new Mock<IBigBrother>();
            var config = new ConfigurationSettings();

            var mockMessageProvider = new Mock<IMessageReceiver>();
            mockMessageProvider.Setup(s => s.ReceiveAsync(
                It.IsAny<int>(),
                It.IsAny<TimeSpan>())).ReturnsAsync(new List<Message> { new Message(Encoding.UTF8.GetBytes("HelloWorld")) });

            var mockMessageProviderFactory = new Mock<IMessageProviderFactory>();
            mockMessageProviderFactory.Setup(s => s.Builder(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>())).Returns(mockMessageProvider.Object);

            var service = new EventReaderService.EventReaderService(
                context, 
                mockedBigBrother.Object,
                mockMessageProviderFactory.Object,
                actorFactory,
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