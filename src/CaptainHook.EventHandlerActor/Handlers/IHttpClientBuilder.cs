using System.Net.Http;
using CaptainHook.Common.Configuration;

namespace CaptainHook.EventHandlerActor.Handlers
{
    public interface IHttpClientBuilder
    {
        HttpClient Build(WebhookConfig config);
    }
}