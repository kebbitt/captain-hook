using System.Threading.Tasks;
using CaptainHook.Common;
using Microsoft.ServiceFabric.Services.Remoting;

namespace CaptainHook.Interfaces
{
    public interface IReaderService : IService
    {
        Task HandlerCompleted(MessageData messageData);
    }
}
