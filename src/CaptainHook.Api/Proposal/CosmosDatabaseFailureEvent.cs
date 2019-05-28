using Eshopworld.Core;
using Microsoft.Azure.Cosmos;

namespace CaptainHook.Api.Proposal
{
    /// <summary>
    /// Event published when the creation of a cosmos database fails.
    /// </summary>
    public class CosmosDatabaseFailureEvent : TelemetryEvent
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CosmosDatabaseFailureEvent"/>.
        /// </summary>
        /// <param name="response">The response payload we got from the Cosmos API.</param>
        public CosmosDatabaseFailureEvent(CosmosDatabaseResponse response)
        {
            DatabaseId = response.Database.Id;
            OperationCost = response.RequestCharge;
        }

        /// <summary>
        /// Gets and sets the ID of the cosmos database that failed during creation.
        /// </summary>
        public string DatabaseId { get; set; }

        /// <summary>
        /// Gets and sets the cost in RUs of the operation to create the database.
        /// </summary>
        public double OperationCost { get; set; }
    }
}
