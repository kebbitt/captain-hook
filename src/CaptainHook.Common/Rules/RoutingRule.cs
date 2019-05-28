using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace CaptainHook.Common.Rules
{
    /// <summary>
    /// Represents a routing rule used to configure captain-hook.
    /// </summary>
    public class RoutingRule
    {
        /// <summary>
        /// A static <see cref="SHA1Managed"/> to hash all the <see cref="RoutingRule"/> key lookups.
        /// </summary>
        internal static readonly SHA1Managed Sha1 = new SHA1Managed();

        /// <summary>
        /// Gets the Rule Key that represents rule uniqueness.
        /// </summary>
        /// <remarks>
        /// The reason why we are spending resources hashing here is to keep the key on a fixed length,
        ///     which will then in turn save time while querying Cosmos and will keep the key in check for RUs
        ///     independently of how big the event type of the target uri are.
        /// </remarks>
        [JsonIgnore]
        public string Key => Convert.ToBase64String(Sha1.ComputeHash(Encoding.UTF8.GetBytes(EventType + HookUri)));

        /// <summary>
        /// The type of the event that we want to create the route for.
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// The target web hook uri that we want to route the event to.
        /// </summary>
        public string HookUri { get; set; }

        /// <summary>
        /// A list of JSONPath based filters on this routing rule.
        /// </summary>
        public IEnumerable<JsonPathFilter> Filters { get; set; }
    }
}
