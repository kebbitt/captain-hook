using System;
using System.Collections.Generic;
using System.Net.Http;
using CaptainHook.Common.Configuration;
using CaptainHook.EventHandlerActor.Handlers;
using Eshopworld.Tests.Core;
using RichardSzalay.MockHttp;
using Xunit;

namespace CaptainHook.Tests.Web.WebHooks
{
    public class HttpClientFactoryTests
    {
        [IsLayer0]
        [Theory]
        [MemberData(nameof(Data))]
        public void CanGetHttpClient(WebhookConfig config)
        {
            var mockHttp = new MockHttpMessageHandler();

            var httpClients = new Dictionary<string, HttpClient> { { new Uri(config.Uri).Host, mockHttp.ToHttpClient() } };

            var httpClientBuilder = new HttpClientFactory(httpClients);

            var httpClient = httpClientBuilder.Get(config);

            Assert.NotNull(httpClient);
        }

        [IsLayer0]
        [Theory]
        [MemberData(nameof(Data))]
        public void CannotGetHttpClient(WebhookConfig config)
        {
            var mockHttp = new MockHttpMessageHandler();

            var httpClients = new Dictionary<string, HttpClient> { { new Uri("https://blahblah.com").Host, mockHttp.ToHttpClient() } };

            var httpClientBuilder = new HttpClientFactory(httpClients);

            Assert.Throws<ArgumentNullException>(() => httpClientBuilder.Get(config));
        }

        public static IEnumerable<object[]> Data =>
            new List<object[]>
            {
                new object[] { new WebhookConfig{Uri = "http://localhost/webhook/post", HttpMethod = HttpMethod.Post }}
            };
    }
}