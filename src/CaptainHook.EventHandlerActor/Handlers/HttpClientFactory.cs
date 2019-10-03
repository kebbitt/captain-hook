using System;
using System.Net.Http;
using Autofac.Features.Indexed;
using CaptainHook.Common.Configuration;

namespace CaptainHook.EventHandlerActor.Handlers
{
    /// <summary>
    /// Builds out a http client for the given request flow
    /// </summary>
    public class HttpClientFactory : IHttpClientFactory
    {
        /// <summary>
        /// Contains an indexed list of http clients which are created from config at startup through config parsing and ioc
        /// </summary>
        private readonly IIndex<string, HttpClient> _httpClients;

        public HttpClientFactory(IIndex<string, HttpClient> httpClients)
        {
            _httpClients = httpClients;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public HttpClient Get(WebhookConfig config)
        {
            var uri = new Uri(config.Uri);

            if (!_httpClients.TryGetValue(uri.Host.ToLowerInvariant(), out var httpClient))
            {
                throw new ArgumentNullException(nameof(httpClient), $"HttpClient for {uri.Host} was not found");
            }
            return httpClient;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public HttpClient Get(string uri)
        {
            if (!_httpClients.TryGetValue(new Uri(uri).Host.ToLowerInvariant(), out var httpClient))
            {
                throw new ArgumentNullException(nameof(httpClient), $"HttpClient for {new Uri(uri).Host} was not found");
            }

            return httpClient;
        }
    }
}