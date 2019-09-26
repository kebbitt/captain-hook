using System.Threading.Tasks;
using CaptainHook.Common;

namespace CaptainHook.Interfaces
{
    using Microsoft.ServiceFabric.Services.Remoting;

    /// <summary>
    /// interface definition for the event dispatcher service
    /// </summary>
    public interface IEventDispatcherService : IService
    {
        Task Dispatch(DispatchRequest request); //TODO: return response object
    }
}
