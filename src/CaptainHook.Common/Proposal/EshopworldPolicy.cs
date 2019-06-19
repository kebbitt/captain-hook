using System;
using System.Net;
using Microsoft.Azure.Cosmos;
using Polly;
using Polly.Retry;

namespace CaptainHook.Common.Proposal
{
    public static class EshopworldPolicy
    {
        public static AsyncRetryPolicy CosmosConflictPolicy()
        {
            return Policy.Handle<CosmosException>(ex => ex.StatusCode == HttpStatusCode.PreconditionFailed)
                         .WaitAndRetryAsync(new[]
                         {
                             TimeSpan.FromSeconds(1),
                             TimeSpan.FromSeconds(2),
                             TimeSpan.FromSeconds(4)
                         });
        }
    }
}
