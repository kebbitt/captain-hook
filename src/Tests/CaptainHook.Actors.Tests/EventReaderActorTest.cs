using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CaptainHook.Common;
using Eshopworld.Telemetry;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using ServiceFabric.Mocks;
using Xunit;

namespace CaptainHook.Actors.Tests
{
    public class EventReaderActorTest
    {
        // TODO: Needs to be refactored to target the service instead
        //[Theory, IsLayer0]
        //[ClassData(typeof(EventReaderActorStateManagerData))]
        //public async Task Foo(IEnumerable<MessageDataHandle> handleList)
        //{
        //    var actorGuid = Guid.NewGuid();
        //    var id = new ActorId(actorGuid.ToString());

        //    ActorBase ActorFactory(ActorService service, ActorId actorId) => new EventReaderActor.EventReaderActor(service, id, new BigBrother("", ""), new ConfigurationSettings());
        //    var svc = MockActorServiceFactory.CreateActorServiceForActor<EventReaderActor.EventReaderActor>(ActorFactory);
        //    var actor = svc.Activate(id);

        //    var stateManager = (MockActorStateManager)actor.StateManager;

        //    var handleData = handleList.ToList();
        //    foreach (var handle in handleData)
        //    {
        //        await stateManager.AddStateAsync(handle.Handle.ToString(), handle);
        //    }

        //    await actor.BuildInMemoryState();

        //    actor.HandlerCount.Should().Be(handleData.Max(h => h.HandlerId));
        //    actor.FreeHandlers.Should().NotContain(handleData.Select(h => h.HandlerId));
        //    actor.LockTokens.Values.Should().BeEquivalentTo(handleData.Select(h => h.LockToken));
        //    actor.InFlightMessages.Values.Should().BeEquivalentTo(handleData.Select(h => h.HandlerId));
        //}

        public class EventReaderActorStateManagerData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                var handle1 = Guid.NewGuid();
                var handle2 = Guid.NewGuid();
                var handle3 = Guid.NewGuid();

                // Three random handlers: 3, 5, 6
                yield return new object[]
                {
                    new[]
                    {
                        new MessageDataHandle
                        {
                            Handle = handle1,
                            HandlerId = 3,
                            LockToken = handle1.ToString()
                        },
                        new MessageDataHandle
                        {
                            Handle = handle2,
                            HandlerId = 5,
                            LockToken = handle2.ToString()
                        },
                        new MessageDataHandle
                        {
                            Handle = handle3,
                            HandlerId = 6,
                            LockToken = handle3.ToString()
                        }
                    }.ToList()
                };

                // Three random handlers, starting at the first ID of the pool: 1, 4, 6
                yield return new object[]
                {
                    new[]
                    {
                        new MessageDataHandle
                        {
                            Handle = handle1,
                            HandlerId = 1,
                            LockToken = handle1.ToString()
                        },
                        new MessageDataHandle
                        {
                            Handle = handle2,
                            HandlerId = 4,
                            LockToken = handle2.ToString()
                        },
                        new MessageDataHandle
                        {
                            Handle = handle3,
                            HandlerId = 6,
                            LockToken = handle3.ToString()
                        }
                    }.ToList()
                };

                // Three consecutive handler IDs at the start of the pool: 1, 2, 3
                yield return new object[]
                {
                    new[]
                    {
                        new MessageDataHandle
                        {
                            Handle = handle1,
                            HandlerId = 1,
                            LockToken = handle1.ToString()
                        },
                        new MessageDataHandle
                        {
                            Handle = handle2,
                            HandlerId = 2,
                            LockToken = handle2.ToString()
                        },
                        new MessageDataHandle
                        {
                            Handle = handle3,
                            HandlerId = 3,
                            LockToken = handle3.ToString()
                        }
                    }.ToList()
                };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
