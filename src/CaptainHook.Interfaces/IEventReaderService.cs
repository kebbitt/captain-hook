using System.Threading.Tasks;
using CaptainHook.Common;
using Microsoft.ServiceFabric.Services.Remoting;

namespace CaptainHook.Interfaces
{
    public interface IEventReaderService : IService
    {
        /// <summary>
        /// Completes a message, deletes the message from the service bus if the message processing is a success
        /// </summary>
        /// <param name="messageData"></param>
        /// <param name="messageSuccess"></param>
        /// <returns></returns>
        Task CompleteMessage(MessageData messageData, bool messageSuccess);
    }
}