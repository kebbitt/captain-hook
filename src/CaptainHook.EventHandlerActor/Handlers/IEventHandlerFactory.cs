namespace CaptainHook.EventHandlerActor.Handlers
{
    /// <summary>
    /// Manages the creation or reuse of specific webhook handler
    /// </summary>
    public interface IEventHandlerFactory
    {
        /// <summary>
        /// Create the custom handler such that we get a mapping from the webhook to the handler selected
        /// </summary>
        /// <param name="fullEventName"></param>
        /// <param name="webhookName">The optional name of a sending webhook handler</param>
        /// <returns></returns>
        IHandler CreateEventHandler(string fullEventName, string webhookName);

        /// <summary>
        /// Used only for getting the callback handler
        /// </summary>
        /// <param name="webHookName"></param>
        /// <returns></returns>
        IHandler CreateWebhookHandler(string webHookName);
    }
}
