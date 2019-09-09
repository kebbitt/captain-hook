using System.Net;
using CaptainHook.Common.Configuration;
using CaptainHook.Common.Telemetry.Web;

namespace CaptainHook.Common.Telemetry
{
    public class FailedWebHookEvent : WebhookEvent
    {
        public FailedWebHookEvent()
        {
        }

        public FailedWebHookEvent(
            string requestHeaders,
            string responseHeaders,
            string requestBody, 
            string responseBody, 
            string eventHandlerActorId,
            string type, 
            string message, 
            string uri, 
            HttpVerb httpVerb, 
            HttpStatusCode statusCode, 
            string correlationId) : base(eventHandlerActorId, type, message, uri, httpVerb, statusCode, correlationId)
        {
            RequestHeaders = requestHeaders;
            ResponseHeaders = responseHeaders;
            RequestBody = requestBody;
            ResponseBody = responseBody;
        }

        public string RequestHeaders { get; set; }

        public string ResponseHeaders { get; set; }

        public string RequestBody { get; set; }

        public string ResponseBody { get; set; }
    }
}