﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ServiceBus.Fluent;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;

namespace CaptainHook.Common
{
    /// <summary>
    /// Contains extensions to the ServiceBus Fluent SDK: <see cref="Microsoft.Azure.Management.ServiceBus.Fluent"/>.
    /// </summary>
    public static class ServiceBusNamespaceExtensions
    {
        /// <summary>
        /// Creates a specific topic if it doesn't exist in the target namespace.
        /// </summary>
        /// <param name="sbNamespace">The <see cref="IServiceBusNamespace"/> where we are creating the topic in.</param>
        /// <param name="name">The name of the topic that we are looking for.</param>
        /// <returns>The <see cref="ITopic"/> entity object that references the Azure topic.</returns>
        public static async Task<ITopic> CreateTopicIfNotExists(this IServiceBusNamespace sbNamespace, string name)
        {
            await sbNamespace.RefreshAsync();

            var topic = (await sbNamespace.Topics.ListAsync()).SingleOrDefault(t => t.Name == name.ToLower());
            if (topic != null) return topic;

            await sbNamespace.Topics
                             .Define(name.ToLower())
                             .WithDuplicateMessageDetection(TimeSpan.FromMinutes(10))
                             .CreateAsync();

            await sbNamespace.RefreshAsync();
            return (await sbNamespace.Topics.ListAsync()).Single(t => t.Name == name.ToLower());
        }

        /// <summary>
        /// Creates a specific subscription to a topic if it doesn't exist yet.
        /// </summary>
        /// <param name="topic">The <see cref="ITopic"/> that we are subscribing to.</param>
        /// <param name="name">The name of the subscription we are doing on the <see cref="ITopic"/>.</param>
        /// <returns>The <see cref="Microsoft.Azure.Management.ServiceBus.Fluent.ISubscription"/> entity object that references the subscription.</returns>
        public static async Task<Microsoft.Azure.Management.ServiceBus.Fluent.ISubscription> CreateSubscriptionIfNotExists(this ITopic topic, string name)
        {
            await topic.RefreshAsync();

            var subscription = (await topic.Subscriptions.ListAsync()).SingleOrDefault(s => s.Name == name.ToLower());
            if (subscription != null) return subscription;

            await topic.Subscriptions
                       .Define(name.ToLower())
                       .WithMessageLockDurationInSeconds(60)
                       .WithExpiredMessageMovedToDeadLetterSubscription()
                       .WithMessageMovedToDeadLetterSubscriptionOnMaxDeliveryCount(10)
                       .CreateAsync();

            await topic.RefreshAsync();
            return (await topic.Subscriptions.ListAsync()).Single(t => t.Name == name.ToLower());
        }

        /// <summary>
        /// Setups a ServiceBus <see cref="ITopic"/> given a subscription Id, a namespace name and the name of the entity we want to work with on the topic.
        /// </summary>
        /// <param name="azureSubscriptionId">The Azure subscription ID where the topic exists.</param>
        /// <param name="serviceBusNamespace">The Azure ServiceBus namespace name.</param>
        /// <param name="entityName">The name of the topic entity that we are working with.</param>
        /// <returns>The <see cref="ITopic"/> contract for use of future operation if required.</returns>
        public static async Task<ITopic> SetupHookTopic(string azureSubscriptionId, string serviceBusNamespace, string entityName)
        {
            var token = new AzureServiceTokenProvider().GetAccessTokenAsync("https://management.core.windows.net/", string.Empty).Result;
            var tokenCredentials = new TokenCredentials(token);

            var client = RestClient.Configure()
                                   .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
                                   .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                                   .WithCredentials(new AzureCredentials(tokenCredentials, tokenCredentials, string.Empty, AzureEnvironment.AzureGlobalCloud))
                                   .Build();

            var sbNamespace = Azure.Authenticate(client, string.Empty)
                                   .WithSubscription(azureSubscriptionId)
                                   .ServiceBusNamespaces.List()
                                   .SingleOrDefault(n => n.Name == serviceBusNamespace);

            if (sbNamespace == null)
            {
                throw new InvalidOperationException($"Couldn't find the service bus namespace {serviceBusNamespace} in the subscription with ID {azureSubscriptionId}");
            }

            return await sbNamespace.CreateTopicIfNotExists(entityName);
        }

    }

    /// <summary>
    /// Contains extensions methods that are <see cref="IServiceBusNamespace"/> related but extend
    /// the system <see cref="Type"/> instead.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Gets a queue/topic name off a system <see cref="Type"/> by using the <see cref="Type"/>.FullName.
        /// If you're in DEBUG with the debugger attached, then the full name is appended by a '-' followed by
        /// an Environment.Username, giving you a named queue during debug cycles to avoid queue name clashes.
        /// </summary>
        /// <param name="type">The message <see cref="Type"/> that this topic is for.</param>
        /// <returns>The final name of the topic.</returns>
        public static string GetEntityName(string type)
        {
            var name = type;
#if DEBUG
            if (Debugger.IsAttached)
            {
                name += $"-{Environment.UserName.Replace("$", "")}";
            }
#endif

            return name?.ToLower();
        }
    }
}