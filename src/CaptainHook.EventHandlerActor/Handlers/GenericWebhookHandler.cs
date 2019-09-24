using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using CaptainHook.Common.Configuration;
using Eshopworld.Core;

namespace CaptainHook.EventHandlerActor.Handlers
{
    /// <summary>
    /// Generic WebHookConfig Handler which executes the call to a webhook based on the supplied configuration
    /// </summary>
    public class GenericWebhookHandler : IHandler
    {
        protected readonly IBigBrother BigBrother;
        protected readonly IHttpClientBuilder HttpClientBuilder;
        protected readonly IRequestBuilder RequestBuilder;
        protected readonly IRequestLogger RequestLogger;
        protected readonly WebhookConfig WebhookConfig;

        public GenericWebhookHandler(
            IHttpClientBuilder httpClientBuilder,
            IRequestBuilder requestBuilder,
            IRequestLogger requestLogger,
            IBigBrother bigBrother,
            WebhookConfig webhookConfig)
        {
            BigBrother = bigBrother;
            RequestBuilder = requestBuilder;
            RequestLogger = requestLogger;
            WebhookConfig = webhookConfig;
            HttpClientBuilder = httpClientBuilder;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="request"></param>
        /// <param name="metadata"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task CallAsync<TRequest>(TRequest request, IDictionary<string, object> metadata, CancellationToken cancellationToken)
        {
            try
            {
                if (!(request is MessageData messageData))
                {
                    throw new Exception("injected wrong implementation");
                }

                //todo refactor into a single call and a dto
                var uri = RequestBuilder.BuildUri(WebhookConfig, messageData.Payload);
                var httpMethod = RequestBuilder.SelectHttpMethod(WebhookConfig, messageData.Payload);
                var payload = RequestBuilder.BuildPayload(this.WebhookConfig, messageData.Payload, metadata);
                var authenticationScheme = RequestBuilder.SelectAuthenticationScheme(WebhookConfig, messageData.Payload);
                var config = RequestBuilder.SelectWebhookConfig(WebhookConfig, messageData.Payload);

                //AuthenticationType authenticationScheme, string correlationId, 
                var httpClient = HttpClientBuilder.Build(config);


                var handler = new HttpFailureLogger(BigBrother, messageData, uri.AbsoluteUri, httpMethod);
                var response = await httpClient.ExecuteAsJsonReliably(httpMethod, uri, payload, handler, token: cancellationToken);

                await RequestLogger.LogAsync(httpClient, response, messageData, uri, httpMethod);
            }
            catch (Exception e)
            {
                BigBrother.Publish(e.ToExceptionEvent());
                throw;
            }
        }
    }
}
