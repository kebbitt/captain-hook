using System.Threading.Tasks;
using CaptainHook.Common;
using Microsoft.ServiceFabric.Services.Remoting;

namespace CaptainHook.Interfaces
{
    public interface IEventReaderService : IService
    {
        Task CompleteMessage(MessageData messageData);
    }
}