using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common.Rules;
using Eshopworld.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace CaptainHook.DirectorService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    public class DirectorService : StatefulService
    {
        internal const string CaptainHookApplicationName = "CaptainHook";
        internal const string ReaderServiceTypeName = "CaptainHook.ReaderServiceType";

        internal readonly IBigBrother Bb;
        internal readonly CosmosContainer RuleContainer;

        internal readonly List<RoutingRule> Rules = new List<RoutingRule>();

        public DirectorService(StatefulServiceContext context, IBigBrother bb, CosmosContainer ruleContainer)
            : base(context)
        {
            Bb = bb;
            RuleContainer = ruleContainer;
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                var iterator = RuleContainer.Items.GetItemIterator<RoutingRule>();
                while (iterator.HasMoreResults)
                {
                    Rules.AddRange(await iterator.FetchNextSetAsync(cancellationToken));
                }

                var uniqueEventTypes = Rules.Select(r => r.EventType).Distinct();
                using (var fabricClient = new FabricClient())
                {
                    var serviceList = (await fabricClient.QueryManager.GetServiceListAsync(new Uri($"fabric:/{CaptainHookApplicationName}")))
                                      .Select(s => s.ServiceName.AbsoluteUri)
                                      .ToList();

                    foreach (var type in uniqueEventTypes)
                    {
                        var serviceNameUri = $"fabric:/{CaptainHookApplicationName}/Reader.{type}";
                        if (serviceList.Contains(serviceNameUri)) continue;

                        await fabricClient.ServiceManager.CreateServiceAsync(new StatefulServiceDescription
                        {
                            ApplicationName = new Uri($"fabric:/{CaptainHookApplicationName}"),
                            HasPersistedState = true,
                            MinReplicaSetSize = 3,
                            TargetReplicaSetSize = 3,
                            PartitionSchemeDescription = new SingletonPartitionSchemeDescription(),
                            ServiceTypeName = ReaderServiceTypeName,
                            ServiceName = new Uri(serviceNameUri)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Bb.Publish(ex.ToExceptionEvent());
                throw;
            }
        }
    }
}