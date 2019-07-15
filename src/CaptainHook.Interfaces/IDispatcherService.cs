using System.Threading.Tasks;
using CaptainHook.Common;
using Microsoft.ServiceFabric.Services.Remoting;

namespace CaptainHook.Interfaces
{
    public interface IDispatcherService : IService
    {
        Task Dispatch(MessageData messageData);
    }
}
