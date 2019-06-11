using System.Net;
using System.Threading.Tasks;
using Autofac.Features.AttributeFilters;
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
    public class RuleSetController : Controller
    {
        private readonly CosmosContainer _ruleSetContainer;
        private readonly CosmosContainer _ruleContainer;

        /// <summary>
        /// Initializes a new instance of <see cref="RuleController"/>.
        /// </summary>
        /// <param name="ruleSetContainer">The injected <see cref="CosmosContainer"/> for <see cref="RuleSet"/></param>
        /// <param name="ruleContainer">The injected <see cref="CosmosContainer"/> for <see cref="RoutingRule"/></param>
        public RuleSetController(
            [KeyFilter(typeof(RuleSet))]     CosmosContainer ruleSetContainer,
            [KeyFilter(typeof(RoutingRule))] CosmosContainer ruleContainer)
        {
            _ruleSetContainer = ruleSetContainer;
            _ruleContainer = ruleContainer;
        }

        /// <summary>
        /// GET implementation for default route
        /// </summary>
        /// <returns>see response code to response type metadata, list of all values</returns>
        [HttpGet]
        [ProducesResponseType(typeof(RuleSet[]), (int) HttpStatusCode.OK)]
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
        [ProducesResponseType(typeof(RuleSet), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Get(string id)
        {
            return await Task.FromResult(new JsonResult("value"));
        }

        /// <summary>
        /// post
        /// </summary>
        /// <param name="rule"></param>
        [HttpPost]
        [ProducesResponseType((int) HttpStatusCode.OK)]
        [ProducesResponseType((int) HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Post([FromBody]RuleSet ruleSet)
        {
            if(!ModelState.IsValid)
                return BadRequest(); // todo: parse errors to payload - proposal ? esw.telemetry ?

            // todo: check existence

            await _ruleSetContainer.Items.CreateItemAsync(ruleSet.PartitionKey, ruleSet);

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
        public async Task<IActionResult> Put(int id, [FromBody]RuleSet ruleSet)
        {
            //if (string.IsNullOrWhiteSpace(value))
            //    return await Task.FromResult(BadRequest());

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
