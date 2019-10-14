﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using CaptainHook.Common.Authentication;
using CaptainHook.Common.Telemetry.Web;
using CaptainHook.EventHandlerActor.Handlers;
using Eshopworld.Core;
using Newtonsoft.Json;

namespace CaptainHook.TokenManagementActor.Handlers.Authentication
{
    /// <summary>
    /// Custom Authentication Handler
    /// </summary>
    public class MmAuthenticationHandler : OidcAuthenticationHandler
    {
        public MmAuthenticationHandler(
            IHttpClientFactory httpClientFactory,
            AuthenticationConfig authenticationConfig,
            IBigBrother bigBrother)
            : base(httpClientFactory, authenticationConfig, bigBrother)
        { }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(OidcAuthenticationConfig.ClientId))
                throw new ArgumentNullException(nameof(OidcAuthenticationConfig.ClientId));

            if (string.IsNullOrEmpty(OidcAuthenticationConfig.ClientSecret))
                throw new ArgumentNullException(nameof(OidcAuthenticationConfig.ClientSecret));

            var httpClient = HttpClientFactory.Get(OidcAuthenticationConfig.Uri);

            var headers = new WebHookHeaders();
            headers.AddContentHeader(Constants.Headers.ContentType, "application/json-patch+json");
            headers.AddRequestHeader("client_id", OidcAuthenticationConfig.ClientId);
            headers.AddRequestHeader("client_secret", OidcAuthenticationConfig.ClientSecret);

            var authProviderResponse = await httpClient.SendRequestReliablyAsync(
                HttpMethod.Post,
                new Uri(OidcAuthenticationConfig.Uri),
                headers,
                string.Empty,
                cancellationToken);

            if (authProviderResponse.StatusCode != HttpStatusCode.Created || authProviderResponse.Content == null)
                throw new Exception("didn't get a token from the provider");

            var responseContent = await authProviderResponse.Content.ReadAsStringAsync();
            var stsResult = JsonConvert.DeserializeObject<OidcAuthenticationToken>(responseContent);

            OidcAuthenticationToken = stsResult;

            BigBrother.Publish(new ClientTokenRequest
            {
                ClientId = OidcAuthenticationConfig.ClientId,
                Authority = OidcAuthenticationConfig.Uri
            });

            return $"Bearer {stsResult.AccessToken}";
        }
    }
}
