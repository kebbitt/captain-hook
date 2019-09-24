using System;
using System.Net;
using CaptainHook.Common.Configuration;

namespace CaptainHook.Common.Telemetry.Web
{
    public class HttpClientFailure : WebhookEvent
    {
        public HttpClientFailure(string eventHandlerActorId, string type, string message, string uri, HttpVerb httpVerb, HttpStatusCode statusCode, string correlationId)
            : base(eventHandlerActorId, type, message, uri, httpVerb, statusCode, correlationId)
        {

        }
    }
}