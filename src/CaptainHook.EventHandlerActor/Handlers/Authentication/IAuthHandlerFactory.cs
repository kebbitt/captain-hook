using System.Threading;
using System.Threading.Tasks;

namespace CaptainHook.EventHandlerActor.Handlers.Authentication
{
    public interface IAuthHandlerFactory
    {
        Task<IAcquireTokenHandler> GetAsync(string name, CancellationToken cancellationToken);
    }
}
