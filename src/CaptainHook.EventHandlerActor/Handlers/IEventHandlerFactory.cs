using System.Threading;
using System.Threading.Tasks;

namespace CaptainHook.EventHandlerActor.Handlers
{
    public interface IEventHandlerFactory
    {
        /// <summary>
        /// Create the custom handler such that we get a mapping from the webhook to the handler selected
        /// </summary>
        /// <param name="fullEventName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IHandler> CreateEventHandlerAsync(string fullEventName, CancellationToken cancellationToken);

        /// <summary>
        /// Used only for getting the callback handler
        /// </summary>
        /// <param name="webHookName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IHandler> CreateWebhookHandlerAsync(string webHookName, CancellationToken cancellationToken);
    }
}
