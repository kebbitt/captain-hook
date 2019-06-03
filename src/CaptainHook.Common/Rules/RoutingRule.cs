using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace CaptainHook.Common.Rules
{
    /// <summary>
    /// Represents a routing rule used to configure captain-hook.
    /// </summary>
    public class RoutingRule
    {
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
        /// The type of the event that we want to create the route for.
        /// </summary>
        [Required]
        public string EventType { get; set; }

        /// <summary>
        /// The target web hook uri that we want to route the event to.
        /// </summary>
        [Required]
        public string HookUri { get; set; }

        /// <summary>
        /// A list of JSONPath based filters on this routing rule.
        /// </summary>
        public IEnumerable<JsonPathFilter> Filters { get; set; }
    }
}
