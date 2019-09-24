using System.Threading.Tasks;
using CaptainHook.Common;
using Microsoft.ServiceFabric.Actors;

namespace CaptainHook.Interfaces
{
    /// <summary>
    /// This interface defines the methods exposed by an actor.
    /// Clients use this interface to interact with the actor that implements it.
    /// </summary>
    public interface IEventHandlerActor : IActor
    {
        /// <summary>
        /// Adds a message to the actors internal state. Timer then picks it up for processing.
        /// </summary>
        /// <param name="messageData"></param>
        /// <returns></returns>
        Task Handle(MessageData messageData);
    }
}
