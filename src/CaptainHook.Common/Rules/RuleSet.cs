﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CaptainHook.Common.Proposal;
using Newtonsoft.Json;

namespace CaptainHook.Common.Rules
{
    /// <summary>
    /// Defines a group of rules to be applied with a single ETag.
    /// </summary>
    public class RuleSet
    {
        /// <summary>
        /// The PartitionKey Path for when creating the collection.
        /// </summary>
        [JsonIgnore]
        public static string PartitionKeyPath => $"/{nameof(Id)}";

        /// <summary>
        /// Gets the partition key for the rule.
        /// </summary>
        [JsonIgnore]
        public string PartitionKey => Id;

        /// <summary>
        /// The ID of the rule set - usually the name of the application setting the rule set.
        /// </summary>
        [Required]
        public string Id { get; set; }

        /// <summary>
        /// Stores the list of previous ETags that we have seen for this rule set.
        /// </summary>
        [HttpIgnore]
        public IEnumerable<string> PreviousETags { get; set; }

        /// <summary>
        /// The ETag that identifies the unique version of this rule set.
        /// </summary>
        [Required]
        public string ETag { get; set; }

        /// <summary>
        /// The list of routing rules that compose this rule set.
        /// </summary>
        [Required]
        public IEnumerable<RoutingRule> RoutingRules { get; set; }
    }
}
