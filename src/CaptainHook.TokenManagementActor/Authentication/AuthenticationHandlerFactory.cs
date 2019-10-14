﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common.Authentication;
using CaptainHook.Common.Configuration;
using Eshopworld.Core;
using JetBrains.Annotations;

namespace CaptainHook.EventHandlerActor.Handlers.Authentication
{
    /// <summary>
    /// Selects the correct authentication handler based on the type specified by the authentication type.
    /// This implemented both Basic, OIDC and a custom implemented which will be moved to an integration layer.
    /// </summary>
    public class AuthenticationHandlerFactory : IAuthenticationHandlerFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConcurrentDictionary<string, IAuthenticationHandler> _handlers;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly IBigBrother _bigBrother;

        public AuthenticationHandlerFactory(
            IHttpClientFactory httpClientFactory,
            IBigBrother bigBrother)
        {
            _bigBrother = bigBrother;
            _httpClientFactory = httpClientFactory;

            _handlers = new ConcurrentDictionary<string, IAuthenticationHandler>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IAuthenticationHandler> GetAsync(WebhookConfig config, CancellationToken cancellationToken)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var key = config.Uri;

            switch (config.AuthenticationConfig.Type)
            {
                case AuthenticationType.None:
                    return null;

                case AuthenticationType.Basic:
                    await EnterSemaphore(() =>
                    {
                        _handlers.TryAdd(key, new BasicAuthenticationHandler(config.AuthenticationConfig));
                    }, key, cancellationToken);
                    break;

                case AuthenticationType.OIDC:
                    await EnterSemaphore(() =>
                    {
                        _handlers.TryAdd(key, new OidcAuthenticationHandler(_httpClientFactory, config.AuthenticationConfig, _bigBrother));
                    }, key, cancellationToken);
                    break;

                case AuthenticationType.Custom:

                    //todo hack for now until we move this out of here and into an integration layer
                    //todo if this is custom it should be another webhook which calls out to another place, this place gets a token on CH's behalf and then adds this into subsequent webhook requests.
                    await EnterSemaphore(() =>
                    {
                        _handlers.TryAdd(key, new MmAuthenticationHandler(_httpClientFactory, config.AuthenticationConfig, _bigBrother));
                    }, key, cancellationToken);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(config.AuthenticationConfig.Type),
                        $"unknown configuration type of {config.AuthenticationConfig.Type}");
            }

            _handlers.TryGetValue(key, out var newHandler);
            return newHandler;
        }

        private async Task EnterSemaphore(Action action, string key, CancellationToken cancellationToken)
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken);

                if (_handlers.ContainsKey(key))
                {
                    return;
                }
                action();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
