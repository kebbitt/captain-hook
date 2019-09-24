using System;
using System.Collections.Concurrent;
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
        /// A list of dynamic http clients which are created as needed depending on endpoints at runtime
        /// </summary>
        private readonly ConcurrentDictionary<string, HttpClient> _dynamicHttpClients;

        /// <summary>
        /// Contains an indexed list of http clients which are created from config at startup through config parsing and ioc
        /// </summary>
        private readonly IIndex<string, HttpClient> _staticHttpClients;

        public HttpClientFactory(IIndex<string, HttpClient> staticHttpClients)
        {
            _staticHttpClients = staticHttpClients;
            _dynamicHttpClients = new ConcurrentDictionary<string, HttpClient>();
        }

        public HttpClient Get(WebhookConfig config)
        {
            var uri = new Uri(config.Uri);

            if (!_staticHttpClients.TryGetValue(uri.Host, out var httpClient))
            {
                throw new ArgumentNullException(nameof(httpClient), $"HttpClient for {uri.Host} was not found");
            }
            return httpClient;
        }

        public HttpClient Get(string uri)
        {
            if (!_dynamicHttpClients.TryGetValue(uri, out var httpClient))
            {
                _dynamicHttpClients.TryAdd(uri, new HttpClient());
            }

            return httpClient;
        }
    }
}