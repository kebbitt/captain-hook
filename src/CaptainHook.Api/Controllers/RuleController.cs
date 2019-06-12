using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Autofac.Features.Indexed;
using CaptainHook.Common.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;

namespace CaptainHook.Api.Controllers
{
    /// <summary>
    /// sample controller
    /// </summary>
    [ApiVersion("1")]
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
        /// GET implementation for default route
        /// </summary>
        /// <returns>see response code to response type metadata, list of all values</returns>
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
        /// Get with Id
        /// </summary>
        /// <param name="eventType"></param>
        /// <returns>see response code to response type metadata, individual value for a given id</returns>
        [HttpGet("{eventType}")]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Get(string eventType)
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
        /// post
        /// </summary>
        /// <param name="rule"></param>
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Post([FromBody]RoutingRule rule)
        {
            if (!ModelState.IsValid)
                return BadRequest(); // todo: parse errors to payload - proposal ? esw.telemetry ?

            // todo: check existence

            await _container.Items.CreateItemAsync(rule.PartitionKey, rule);

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
        public async Task<IActionResult> Put(int id, [FromBody]string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return await Task.FromResult(BadRequest());

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
