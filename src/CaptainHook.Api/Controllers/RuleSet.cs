using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CaptainHook.Common.Rules;

namespace CaptainHook.Api.Controllers
{
    /// <summary>
    /// Defines a group of rules to be applied with a single ETag.
    /// </summary>
    public class RuleSet
    {
        /// <summary>
        /// The ID of the rule set - usually the name of the application setting the rule set.
        /// </summary>
        [Required]
        public string Id { get; set; }

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
