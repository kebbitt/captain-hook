using System;
using System.Net;
using Eshopworld.Core;
using Microsoft.Azure.Cosmos;

namespace CaptainHook.Api.Proposal
{
    /// <summary>
    /// Contains extension methods for Cosmos v3 SDK responses.
    /// </summary>
    public static class CosmosResponseExtensions
    {
        /// <summary>
        /// Handles a response from a Cosmos Database Create call.
        ///     It will exit the process on failure, so do not use this if you can recover from a failed database event (most applications can't).
        /// </summary>
        /// <param name="response">The response given by the Cosmos API.</param>
        /// <param name="bb">The instance of <see cref="IBigBrother"/> we are using to log the event.</param>
        /// <returns>The <see cref="CosmosDatabase"/> object on a successful creation call.</returns>
        public static CosmosDatabase HandleResponse(this CosmosDatabaseResponse response, IBigBrother bb)
        {
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                bb.Publish(new CosmosDatabaseFailureEvent(response));
                bb.Flush(); // Flush because we are triggering exit right after
                Environment.Exit(ExitCode.Cosmos.DatabaseCreationFailure);
            }

            return response.Database;
        }

        /// <summary>
        /// Handles a response from a Cosmos Container Create call.
        ///     It will exit the process on failure, so do not use this if you can recover from a failed database event (most applications can't).
        /// </summary>
        /// <param name="response">The response given by the Cosmos API.</param>
        /// <param name="bb">The instance of <see cref="IBigBrother"/> we are using to log the event.</param>
        /// <returns>The <see cref="CosmosContainer"/> object on a successful creation call.</returns>
        public static CosmosContainer HandleResponse(this CosmosContainerResponse response, IBigBrother bb)
        {
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                bb.Publish(new CosmosContainerFailureEvent(response));
                bb.Flush(); // Flush because we are triggering exit right after
                Environment.Exit(ExitCode.Cosmos.ContainerCreationFailure);
            }

            return response.Container;
        }
    }
}
