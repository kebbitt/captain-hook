using System;
using System.Fabric;
using ServiceFabric.Mocks;

namespace CaptainHook.Tests.Services
{
    /// todo move to a common package or issue PR to mocks library
    /// <summary>
    /// Mocking Helper for the StatefulServiceContextFactory
    /// </summary>
    public class CustomMockStatefulServiceContextFactory : MockStatefulServiceContextFactory
    {
        private static readonly Random Random = new Random();

        public static StatefulServiceContext Create(string serviceTypeName, string serviceName, byte[] initializationData, string partitionId = "D9C5DA21-499B-458B-9B04-3EB7B44AE7AE", long? replicaId=null)
        {
            return new StatefulServiceContext(
                new NodeContext("Node0", new NodeId(0, 1), 0, "NodeType1", "localhost"), 
                MockCodePackageActivationContext.Default, 
                serviceTypeName, 
                new Uri(serviceName), 
                initializationData, 
                Guid.Parse(partitionId), 
                replicaId ?? Random.Next());
        }
    }
}
