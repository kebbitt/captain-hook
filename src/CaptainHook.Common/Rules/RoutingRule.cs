﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using CaptainHook.Common.Proposal;
using Newtonsoft.Json;

namespace CaptainHook.Common.Rules
{
    /// <summary>
    /// Represents a routing rule used to configure captain-hook.
    /// </summary>
    /// <remarks>
    /// TODO: Authentication scheme
    /// TODO: Protect against casing
    /// </remarks>
    public class RoutingRule : IEqualityComparer<RoutingRule>
    {
        private readonly SHA256Managed _sha = new SHA256Managed();
        private string _id;

        /// <summary>
        /// Initializes a new instance of <see cref="RoutingRule"/>.
        /// </summary>
        public RoutingRule()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RoutingRule"/>.
        /// </summary>
        /// <param name="eventType">The type of the event on this rule.</param>
        /// <param name="hookUri">The target hook URI for this rule.</param>
        public RoutingRule(string eventType, string hookUri)
        {
            EventType = eventType;
            HookUri = hookUri;
        }

        /// <summary>
        /// The PartitionKey Path for when creating the collection.
        /// </summary>
        [JsonIgnore]
        public static string PartitionKeyPath  => $"/{nameof(EventType)}";

        /// <summary>
        /// Gets the partition key for the rule.
        /// </summary>
        [JsonIgnore]
        public string PartitionKey => EventType;

        /// <summary>
        /// Gets the Id of the document, that is a hashed composite key with the <see cref="EventType"/> and the <see cref="HookUri"/>.
        /// </summary>
        [HttpIgnore, JsonProperty(PropertyName = "id")]
        public string Id => _id ?? (_id = Encoding.UTF8.GetString(_sha.ComputeHash(Encoding.UTF8.GetBytes(EventType + HookUri))));

        /// <summary>
        /// The type of the event that we want to create the route for.
        /// </summary>
        [Required]
        public string EventType { get; set; }

        /// <summary>
        /// The target web hook uri that we want to route the event to.
        /// </summary>
        [Required]
        public string HookUri { get; set; }


        public string Callback { get; set; }

        /// <summary>
        /// A list of JSONPath based filters on this routing rule.
        /// </summary>
        public IEnumerable<JsonPathFilter> Filters { get; set; }

        /// <inheritdoc />
        public bool Equals(RoutingRule x, RoutingRule y)
        {
            return x.EventType == y.EventType && x.HookUri == y.HookUri;
        }

        /// <inheritdoc />
        public int GetHashCode(RoutingRule obj)
        {
            unchecked
            {
                var hash = (int)2166136261;

                if (EventType != null) hash = (hash * 16777619) ^ EventType.GetHashCode();
                if (HookUri != null) hash = (hash * 16777619) ^ HookUri.GetHashCode();

                return hash;
            }
        }
    }
}
