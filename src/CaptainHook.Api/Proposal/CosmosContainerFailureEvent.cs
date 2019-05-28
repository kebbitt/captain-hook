using Eshopworld.Core;
using Microsoft.Azure.Cosmos;

namespace CaptainHook.Api.Proposal
{
    /// <summary>
    /// Event published when the creation of a cosmos container fails.
    /// </summary>
    public class CosmosContainerFailureEvent : TelemetryEvent
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CosmosContainerFailureEvent"/>.
        /// </summary>
        /// <param name="response">The response payload we got from the Cosmos API.</param>
        public CosmosContainerFailureEvent(CosmosContainerResponse response)
        {
            ContainerId = response.Container.Id;
            OperationCost = response.RequestCharge;
        }

        /// <summary>
        /// Gets and sets the ID of the cosmos container that failed during creation.
        /// </summary>
        public string ContainerId { get; set; }

        /// <summary>
        /// Gets and sets the cost in RUs of the operation to create the database.
        /// </summary>
        public double OperationCost { get; set; }
    }
}
