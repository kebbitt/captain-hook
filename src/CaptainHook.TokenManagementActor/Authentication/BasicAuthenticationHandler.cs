﻿using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common.Authentication;

namespace CaptainHook.TokenManagementActor.Handlers.Authentication
{
    /// <summary>
    /// Basic Authentication Handler which returns a http client with a basic http authentication header
    /// </summary>
    public class BasicAuthenticationHandler : AuthenticationHandler, IAuthenticationHandler
    {
        protected readonly BasicAuthenticationConfig BasicAuthenticationConfig;

        public BasicAuthenticationHandler(AuthenticationConfig authenticationConfig)
        {
            var basicAuthenticationConfig = authenticationConfig as BasicAuthenticationConfig;

            BasicAuthenticationConfig = basicAuthenticationConfig ?? throw new ArgumentException($"configuration for basic authentication is not of type {typeof(BasicAuthenticationConfig)}", nameof(authenticationConfig));
        }

        /// <inheritdoc />
        /// <summary>
        /// Gets a token and updates the http client with the authentication header
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            var encodedValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{BasicAuthenticationConfig.Username}:{BasicAuthenticationConfig.Password}"));
            var token = $"Basic {encodedValue}";

            return await Task.FromResult(token);
        }
    }
}
