using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Autofac.Features.Indexed;
using CaptainHook.Common.Proposal;
using CaptainHook.Common.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;

namespace CaptainHook.Api.Controllers
{
    /// <summary>
    /// Deals with operations for <see cref="RoutingRuleSet"/>.
    /// </summary>
    /// <remarks>
    /// Intended to be used by internal tools that want to off-load detailed rules management.
    /// </remarks>
    [ApiController, ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Produces("application/json")]
    public class RuleSetController : Controller
    {
        private readonly CosmosContainer _ruleSetContainer;
        private readonly CosmosContainer _ruleContainer;

        /// <summary>
        /// Initializes a new instance of <see cref="RuleController"/>.
        /// </summary>
        /// <param name="containers">The injected <see cref="Dictionary{TKey,TValue}"/> of <see cref="CosmosContainer"/>.</param>
        public RuleSetController(IIndex<string, CosmosContainer> containers)
        {
            _ruleSetContainer = containers[nameof(RoutingRuleSet)];
            _ruleContainer = containers[nameof(RoutingRule)];
        }

        /// <summary>
        /// GET implementation for default route.
        /// </summary>
        /// <returns>The full list of <see cref="RoutingRuleSet"/> in the captain-hook platform.</returns>
        /// <remarks>
        /// Currently doesn't implement any paging mechanisms.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(RoutingRuleSet[]), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Get()
        {
            var result = new List<RoutingRuleSet>();

            var iterator = _ruleSetContainer.Items.GetItemIterator<RoutingRuleSet>();
            while (iterator.HasMoreResults)
            {
                result.AddRange(await iterator.FetchNextSetAsync());
            }

            return new JsonResult(result);
        }

        /// <summary>
        /// Get implementation for a <see cref="RoutingRuleSet"/> with a specific ID.
        /// </summary>
        /// <param name="id">The ID of the <see cref="RoutingRuleSet"/> that we are retrieving.</param>
        /// <returns>The <see cref="RoutingRuleSet"/> with the given ID.</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(RoutingRuleSet), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Get(string id)
        {
            var readResponse = await _ruleSetContainer.Items.ReadItemAsync<RoutingRuleSet>(id, id);
            return readResponse.StatusCode == HttpStatusCode.NotFound ? (IActionResult) NotFound() : new JsonResult(readResponse.Resource);
        }

        /// <summary>
        /// Implementation of the POST HTTP verb for <see cref="RoutingRuleSet"/>.
        ///     Creates a <see cref="RoutingRuleSet"/>.
        /// </summary>
        /// <param name="routingRuleSet">The <see cref="RoutingRuleSet"/> we want to create.</param>
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Conflict)]
        public async Task<IActionResult> Post([FromBody]RoutingRuleSet routingRuleSet)
        {
            RoutingRuleSet previousSet = default;
            var result = await EshopworldPolicy.CosmosConflictPolicy().ExecuteAsync<IActionResult>(async () =>
            {
                var readResponse = await _ruleSetContainer.Items.ReadItemAsync<RoutingRuleSet>(routingRuleSet.PartitionKey, routingRuleSet.Id);
                if (readResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    previousSet = readResponse.Resource;

                    if (previousSet.ETag == routingRuleSet.ETag)
                    {
                        return Ok();
                    }

                    if (previousSet.PreviousETags.Contains(routingRuleSet.ETag))
                    {
                        return Conflict();
                    }

                    routingRuleSet.PreviousETags = previousSet.PreviousETags.Concat(new[] { previousSet.ETag });

                    await _ruleSetContainer.Items.ReplaceItemAsync(
                        routingRuleSet.PartitionKey,
                        routingRuleSet.Id,
                        routingRuleSet,
                        new CosmosItemRequestOptions
                        {
                            AccessCondition = new AccessCondition {Type = AccessConditionType.IfMatch, Condition = readResponse.ETag}
                        }
                    );

                    return null;
                }

                await _ruleSetContainer.Items.CreateItemAsync(routingRuleSet.PartitionKey, routingRuleSet);
                return null;
            });

            if (result != null) return result;

            var addRules = previousSet == null
                ? routingRuleSet.RoutingRules.ToList()
                : routingRuleSet.RoutingRules.Except(previousSet.RoutingRules).ToList();

            var removeRules = previousSet == null
                ? new List<RoutingRule>()
                : previousSet.RoutingRules.Except(routingRuleSet.RoutingRules).ToList();

            var replaceRules = previousSet == null
                ? new List<RoutingRule>()
                : previousSet.RoutingRules.Except(routingRuleSet.RoutingRules).ToList();

            // Create
            await EshopworldPolicy.CosmosConflictPolicy().ExecuteAsync(async() =>
            {
                foreach (var rule in addRules)
                {
                    await _ruleContainer.Items.CreateItemAsync(rule.PartitionKey, rule);
                }
            });

            // Delete
            await EshopworldPolicy.CosmosConflictPolicy().ExecuteAsync(async () =>
            {
                foreach (var rule in removeRules)
                {
                    await _ruleContainer.Items.DeleteItemAsync<RoutingRule>(rule.PartitionKey, rule.Id);
                }
            });

            // Replace || TODO? Should we handle other types of responses like 404 ?
            await EshopworldPolicy.CosmosConflictPolicy().ExecuteAsync(async () =>
            {
                foreach (var rule in replaceRules)
                {
                    await _ruleContainer.Items.ReplaceItemAsync(rule.PartitionKey, rule.Id, rule);
                }
            });

            return Ok();
        }

        /// <summary>
        /// Implementation of the DELETE HTTP verb for <see cref="RoutingRuleSet"/>.
        ///     Deletes a <see cref="RoutingRuleSet"/> given it's key.
        ///     It also removes all <see cref="RoutingRule"/> associated with the set.
        /// </summary>
        /// <param name="id">The ID of the <see cref="RoutingRuleSet"/> that we are deleting.</param>
        /// <remarks>
        /// This method is a hard delete, intended for cleanup purposes only.
        ///     Once the rule set is deleted, the knowledge of previous ETags is deleted also, that resets
        ///     the entire history of the rule set.
        /// </remarks>
        [HttpDelete("{id}")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Delete(string id)
        {
            var readResponse = await _ruleSetContainer.Items.ReadItemAsync<RoutingRuleSet>(id, id);

            if (readResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            var ruleSet = readResponse.Resource;
            var deleteResponse = await _ruleSetContainer.Items.DeleteItemAsync<RoutingRuleSet>(id, id);

            if (deleteResponse.StatusCode == HttpStatusCode.OK)
            {
                foreach (var rule in ruleSet.RoutingRules)
                {
                    await _ruleContainer.Items.DeleteItemAsync<RoutingRule>(rule.PartitionKey, rule.Id);
                }
            }

            return Ok();
        }
    }
}
