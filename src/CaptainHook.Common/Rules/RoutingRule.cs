using System.Collections.Generic;
using System.Security.Cryptography;

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
        public static string PartitionKey  => $"/{nameof(EventType)}";

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
