using System;
using System.Net;
using System.Net.Http;
using CaptainHook.Common;
using CaptainHook.Common.Telemetry.Web;
using Eshopworld.Core;

namespace CaptainHook.EventHandlerActor.Handlers
{
    /// <summary>
    /// Wrapper for logging http failures in CH which include data re the endpoint being hit and the event from which the request came
    /// </summary>
    [Obsolete("Need to wrap the http handlers in a polly rather than wrap the retries in a http client")]
    public class HttpFailureLogger
    {
        private readonly IBigBrother _bigBrother;
        private readonly MessageData _message;
        private readonly string _uri;
        private readonly HttpMethod _httpMethod;

        public HttpFailureLogger(IBigBrother bigBrother, MessageData message, string uri, HttpMethod httpMethod)
        {
            _bigBrother = bigBrother;
            _message = message;
            _uri = uri;
            _httpMethod = httpMethod;
        }

        public void Publish(string message, HttpStatusCode statusCode, string correlationId)
        {
            _bigBrother.Publish(new HttpClientFailure(_message.EventHandlerActorId, _message.Type, message, _uri, _httpMethod, statusCode, correlationId));
        }
    }
}