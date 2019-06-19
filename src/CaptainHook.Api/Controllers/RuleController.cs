using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.Tasks;
using Autofac.Features.Indexed;
using CaptainHook.Common.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;

namespace CaptainHook.Api.Controllers
{
    /// <summary>
    /// Deals with operations for <see cref="RoutingRule"/>.
    /// </summary>
    /// <remarks>
    /// Intended to be used by tools and components that implement rules from external 3rd parties, like retailers.
    /// </remarks>
    [ApiController, ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Produces("application/json")]
    public class RuleController : Controller
    {
        private readonly CosmosContainer _container;

        /// <summary>
        /// Initializes a new instance of <see cref="RuleController"/>.
        /// </summary>
        /// <param name="containers">The injected <see cref="CosmosContainer"/></param>
        public RuleController(IIndex<string, CosmosContainer> containers)
        {
            _container = containers[nameof(RoutingRule)];
        }

        /// <summary>
        /// GET implementation for default route.
        /// </summary>
        /// <returns>The full list of Rules in the EDA platform.</returns>
        /// <remarks>
        /// Currently doesn't implement any paging mechanisms.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(string[]), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Get()
        {
            var result = new List<RoutingRule>();

            var iterator = _container.Items.GetItemIterator<RoutingRule>();
            while (iterator.HasMoreResults)
            {
                result.AddRange(await iterator.FetchNextSetAsync());
            }

            return new JsonResult(result);
        }

        /// <summary>
        /// Get implementation for the full <see cref="RoutingRule"/> partition, based on the type of event.
        /// </summary>
        /// <param name="eventType">The type of the event key part of the rule.</param>
        /// <returns>The full list of Rules for the given event type.</returns>
        /// <remarks>
        /// Currently doesn't implement any paging mechanisms.
        /// </remarks>
        [HttpGet("{eventType}")]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Get([Required]string eventType)
        {
            var result = new List<RoutingRule>();

            var iterator = _container.Items.CreateItemQuery<RoutingRule>(new CosmosSqlQueryDefinition($"SELECT * FROM {nameof(RoutingRule)}"), eventType);
            while (iterator.HasMoreResults)
            {
                result.AddRange(await iterator.FetchNextSetAsync());
            }

            return new JsonResult(result);
        }

        /// <summary>
        /// Implementation of the POST HTTP verb for <see cref="RoutingRule"/>.
        ///     Creates a <see cref="RoutingRule"/>.
        /// </summary>
        /// <param name="rule">The <see cref="RoutingRule"/> we want to create.</param>
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Conflict)]
        public async Task<IActionResult> Post([FromBody]RoutingRule rule)
        {
            var readResponse = await _container.Items.ReadItemAsync<RoutingRule>(rule.EventType, rule.Id);
            if (readResponse.Resource != null && readResponse.StatusCode == HttpStatusCode.OK)
            {
                return Conflict("Routing Rule already exists");
            }

            await _container.Items.CreateItemAsync(rule.PartitionKey, rule);

            return Ok();
        }

        /// <summary>
        /// Implementation of the PUT HTTP verb for <see cref="RoutingRule"/>.
        ///     Updates a <see cref="RoutingRule"/>.
        /// </summary>
        /// <param name="rule">The <see cref="RoutingRule"/> we want to update.</param>
        [HttpPut]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Put([FromBody]RoutingRule rule)
        {
            var readResponse = await _container.Items.ReadItemAsync<RoutingRule>(rule.EventType, rule.Id);
            if (readResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            await _container.Items.ReplaceItemAsync(rule.PartitionKey, rule.Id, rule);

            return Ok();
        }

        /// <summary>
        /// Implementation of the DELETE HTTP verb for <see cref="RoutingRule"/>.
        ///     Deletes a <see cref="RoutingRule"/> given it's composite key.
        /// </summary>
        /// <param name="eventType">The type of the event key part of the rule.</param>
        /// <param name="hookUri">The hook URI key part of the rule.</param>
        [HttpDelete("{eventType}/{hookUri}")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Delete([Required]string eventType, [Required]string hookUri)
        {
            var deleteResponse = await _container.Items.DeleteItemAsync<RoutingRule>(eventType, new RoutingRule(eventType, hookUri).Id);
            return deleteResponse.StatusCode == HttpStatusCode.NotFound ? (IActionResult) NotFound() : Ok();
        }
    }
}
