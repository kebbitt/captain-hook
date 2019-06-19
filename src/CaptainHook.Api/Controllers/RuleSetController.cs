using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Autofac.Features.Indexed;
using CaptainHook.Common.Proposal;
using CaptainHook.Common.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Polly;

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
        /// GET implementation for default route
        /// </summary>
        /// <returns>see response code to response type metadata, list of all values</returns>
        [HttpGet]
        [ProducesResponseType(typeof(RoutingRuleSet[]), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Get()
        {
            return await Task.FromResult(new JsonResult(new[] { "value1", "value2" }));
        }

        /// <summary>
        /// Get with Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>see response code to response type metadata, individual value for a given id</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(RoutingRuleSet), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Get(string id)
        {
            return await Task.FromResult(new JsonResult("value"));
        }

        /// <summary>
        /// Implementation of the POST HTTP verb for <see cref="RoutingRuleSet"/>.
        ///     Creates a <see cref="RoutingRuleSet"/>.
        /// </summary>
        /// <param name="routingRuleSet">The <see cref="RoutingRuleSet"/> we want to create.</param>
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
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
        /// Put
        /// </summary>
        /// <param name="id">id to process</param>
        /// <param name="value">payload</param>
        /// <returns>action result</returns>
        [HttpPut("{id}")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Put(int id, [FromBody]RoutingRuleSet routingRuleSet)
        {
            return await Task.FromResult(Ok());
        }

        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="id">id to delete</param>
        /// <returns>action result</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            return await Task.FromResult(Ok());
        }
    }
}
