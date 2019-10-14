﻿using System.Net;
using System.Net.Http;

namespace CaptainHook.Common.Telemetry.Web
{
    public class HttpClientFailure : WebhookEvent
    {
        public HttpClientFailure(string eventHandlerActorId, string type, string message, string uri, HttpMethod httpMethod, HttpStatusCode statusCode, string correlationId)
            : base(eventHandlerActorId, type, message, uri, httpMethod, statusCode, correlationId)
        {

        }
    }
}