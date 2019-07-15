using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using CaptainHook.Common.Rules;
using Eshopworld.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace CaptainHook.DirectorService
{
    /// <summary>
    /// An instance of this class is created for each Director service replica.
    ///     Represents a single director for the entire CaptainHook stack.
    /// </summary>
    public class DirectorService : StatefulService
    {
        internal readonly IBigBrother Bb;
        internal readonly CosmosContainer RuleContainer;
        internal readonly FabricClient FabricClient;

        internal readonly List<RoutingRule> Rules = new List<RoutingRule>();

        /// <summary>
        /// Initializes a new instance of <see cref="DirectorService"/>.
        /// </summary>
        /// <param name="context">The injected <see cref="StatefulServiceContext"/>.</param>
        /// <param name="bb">The injected <see cref="IBigBrother"/> telemetry interface.</param>
        /// <param name="ruleContainer">The injected <see cref="CosmosContainer"/> for the <see cref="RoutingRule"/>.</param>
        /// <param name="fabricClient">The injected <see cref="FabricClient"/>.</param>
        public DirectorService(StatefulServiceContext context, IBigBrother bb, CosmosContainer ruleContainer, FabricClient fabricClient)
            : base(context)
        {
            Bb = bb;
            RuleContainer = ruleContainer;
            FabricClient = fabricClient;
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Check fabric node topology - if running below Bronze, set min and target replicas to 1 instead of 3

            try
            {
                var iterator = RuleContainer.Items.GetItemIterator<RoutingRule>();
                while (iterator.HasMoreResults)
                {
                    Rules.AddRange(await iterator.FetchNextSetAsync(cancellationToken));
                }

                var uniqueEventTypes = Rules.Select(r => r.EventType).Distinct();
                var serviceList = (await FabricClient.QueryManager.GetServiceListAsync(new Uri($"fabric:/{CaptainHookApplication.ApplicationName}")))
                                  .Select(s => s.ServiceName.AbsoluteUri)
                                  .ToList();

                foreach (var type in uniqueEventTypes)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var readerServiceNameUri = $"fabric:/{CaptainHookApplication.ApplicationName}/{CaptainHookApplication.ReaderServicePrefix}.{type}";
                    if (!serviceList.Contains(readerServiceNameUri))
                    {
                        await FabricClient.ServiceManager.CreateServiceAsync(
                            new StatefulServiceDescription
                            {
                                ApplicationName = new Uri($"fabric:/{CaptainHookApplication.ApplicationName}"),
                                HasPersistedState = true,
                                MinReplicaSetSize = 3,
                                TargetReplicaSetSize = 3,
                                PartitionSchemeDescription = new SingletonPartitionSchemeDescription(),
                                ServiceTypeName = CaptainHookApplication.ReaderServiceType,
                                ServiceName = new Uri(readerServiceNameUri),
                                InitializationData = Encoding.UTF8.GetBytes(type)
                            });
                    }

                    var handlerServiceNameUri = $"fabric:/{CaptainHookApplication.ApplicationName}/{CaptainHookApplication.HandlerServicePrefix}.{type}";
                    if (!serviceList.Contains(handlerServiceNameUri))
                    {
                        // TODO: Untested - so commented out - not sure if actor services are exactly like stateful services
                        //await FabricClient.ServiceManager.CreateServiceAsync(
                        //    new StatefulServiceDescription
                        //    {
                        //        ApplicationName = new Uri($"fabric:/{CaptainHookApplication.ApplicationName}"),
                        //        HasPersistedState = true,
                        //        MinReplicaSetSize = 3,
                        //        TargetReplicaSetSize = 3,
                        //        PartitionSchemeDescription = new SingletonPartitionSchemeDescription(),
                        //        ServiceTypeName = CaptainHookApplication.HandlerServiceType,
                        //        ServiceName = new Uri(handlerServiceNameUri)
                        //    });
                    }
                }

                // TODO: Can't do this for internal eshopworld.com|net hosts, otherwise the sharding would be crazy - need to aggregate internal hosts by domain
                var uniqueHosts = Rules.Select(r => new Uri(r.HookUri).Host).Distinct();
                var dispatcherServiceList = (await FabricClient.QueryManager.GetServiceListAsync(new Uri($"fabric:/{CaptainHookApplication.ApplicationName}")))
                                        .Select(s => s.ServiceName.AbsoluteUri)
                                        .ToList();

                foreach (var host in uniqueHosts)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var dispatcherServiceNameUri = $"fabric:/{CaptainHookApplication.ApplicationName}/{CaptainHookApplication.DispatcherServicePrefix}.{host}";
                    if (dispatcherServiceList.Contains(dispatcherServiceNameUri)) continue;

                    await FabricClient.ServiceManager.CreateServiceAsync(
                        new StatefulServiceDescription
                        {
                            ApplicationName = new Uri($"fabric:/{CaptainHookApplication.ApplicationName}"),
                            HasPersistedState = true,
                            MinReplicaSetSize = 3,
                            TargetReplicaSetSize = 3,
                            PartitionSchemeDescription = new SingletonPartitionSchemeDescription(),
                            ServiceTypeName = CaptainHookApplication.ReaderServiceType,
                            ServiceName = new Uri(dispatcherServiceNameUri),
                            InitializationData = Encoding.UTF8.GetBytes(host)
                        });
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