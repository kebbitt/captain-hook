using System;
using System.Net;
using Microsoft.Azure.Cosmos;

namespace CaptainHook.Api.Proposal
{
    public static class CosmosExtensions
    {
        public static CosmosDatabase HandleResponse(this CosmosDatabaseResponse response)
        {
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
                Environment.Exit(ExitCode.Cosmos.DatabaseCreationFailure);

            return response.Database;
        }

        public static CosmosContainer HandleResponse(this CosmosContainerResponse response)
        {
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
                Environment.Exit(ExitCode.Cosmos.ContainerCreationFailure);

            return response.Container;
        }
    }
}
