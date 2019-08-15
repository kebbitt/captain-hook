using System;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Services.Runtime;

namespace CaptainHook.DirectorService
{
    public class DirectorService : StatefulService
    {
        internal readonly IBigBrother BigBrother;
        internal readonly FabricClient FabricClient;

        /// <summary>
        /// Initializes a new instance of <see cref="DirectorService"/>.
        /// </summary>
        /// <param name="context">The injected <see cref="StatefulServiceContext"/>.</param>
        /// <param name="bigBrother">The injected <see cref="IBigBrother"/> telemetry interface.</param>
        /// <param name="fabricClient">The injected <see cref="FabricClient"/>.</param>
        public DirectorService(StatefulServiceContext context, IBigBrother bigBrother, FabricClient fabricClient)
            : base(context)
        {
            BigBrother = bigBrother;
            FabricClient = fabricClient;
            var config = context.CodePackageActivationContext.GetConfigurationPackageObject("DefaultServiceConfig");
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
                //var iterator = RuleContainer.Items.GetItemIterator<RoutingRule>();
                //while (iterator.HasMoreResults)
                //{
                //    Rules.AddRange(await iterator.FetchNextSetAsync(cancellationToken));
                //}

                //var uniqueEventTypes = Rules.Select(r => r.EventType).Distinct();

                var events = new[]
                {
                    "Core.Events.Test.TrackingdDomainEvent",
                    //"checkout.domain.infrastructure.domainevents.retailerorderconfirmationdomainevent",
                    //"checkout.domain.infrastructure.domainevents.platformordercreatedomainevent",
                    //"nike.snkrs.core.events.productrefreshevent",
                    //"nike.snkrs.core.events.productupdatedevent",
                    //"nike.snkrs.controltowerapi.models.events.nikelaunchdatareceivedevent",
                    //"bullfrog.domainevents.scalechange"
                };

                var serviceList = (await FabricClient.QueryManager.GetServiceListAsync(new Uri($"fabric:/{Constants.CaptainHookApplication.ApplicationName}")))
                                  .Select(s => s.ServiceName.AbsoluteUri)
                                  .ToList();

                foreach (var type in events)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    //todo make the names of the types PascalCaseing before they are created.
                    var readerServiceNameUri = $"fabric:/{Constants.CaptainHookApplication.ApplicationName}/{Constants.CaptainHookApplication.Services.EventReaderServicePrefix}.{type}";
                    if (!serviceList.Contains(readerServiceNameUri))
                    {
                        await FabricClient.ServiceManager.CreateServiceAsync(
                            new StatefulServiceDescription
                            {
                                ApplicationName = new Uri($"fabric:/{Constants.CaptainHookApplication.ApplicationName}"),
                                HasPersistedState = true,
                                MinReplicaSetSize = 3,
                                TargetReplicaSetSize = 3,
                                PartitionSchemeDescription = new SingletonPartitionSchemeDescription(),
                                ServiceTypeName = Constants.CaptainHookApplication.Services.EventReaderServiceType,
                                ServiceName = new Uri(readerServiceNameUri),
                                InitializationData = Encoding.UTF8.GetBytes(type)
                            });
                    }

                    //var handlerServiceNameUri = $"fabric:/{Constants.CaptainHookApplication.ApplicationName}/{Constants.CaptainHookApplication.Services.EventHandlerServicePrefix}.{type}";
                    //if (!serviceList.Contains(handlerServiceNameUri))
                    //{
                    //    // TODO: Untested - so commented out - not sure if actor services are exactly like stateful services
                    //    //await FabricClient.ServiceManager.CreateServiceAsync(
                    //    //    new StatefulServiceDescription
                    //    //    {
                    //    //        ApplicationName = new Uri($"fabric:/{CaptainHookApplication.ApplicationName}"),
                    //    //        HasPersistedState = true,
                    //    //        MinReplicaSetSize = 3,
                    //    //        TargetReplicaSetSize = 3,
                    //    //        PartitionSchemeDescription = new SingletonPartitionSchemeDescription(),
                    //    //        ServiceTypeName = CaptainHookApplication.EventHandlerServiceType,
                    //    //        ServiceName = new Uri(handlerServiceNameUri)
                    //    //    });
                    //}
                }

                // TODO: Can't do this for internal eshopworld.com|net hosts, otherwise the sharding would be crazy - need to aggregate internal hosts by domain
                //var uniqueHosts = Rules.Select(r => new Uri(r.HookUri).Host).Distinct();
                //var dispatcherServiceList = (await FabricClient.QueryManager.GetServiceListAsync(new Uri($"fabric:/{Constants.CaptainHookApplication.ApplicationName}")))
                //                        .Select(s => s.ServiceName.AbsoluteUri)
                //                        .ToList();

                //todo this might be used for dispatchers per host but that seems a bit drastic
                //foreach (var host in uniqueHosts)
                //{
                //    if (cancellationToken.IsCancellationRequested) return;

                //    var dispatcherServiceNameUri = $"fabric:/{Constants.CaptainHookApplication.ApplicationName}/{Constants.CaptainHookApplication.EventDispatcherServicePrefix}.{host}";
                //    if (dispatcherServiceList.Contains(dispatcherServiceNameUri)) continue;

                //    await FabricClient.ServiceManager.CreateServiceAsync(
                //        new StatefulServiceDescription
                //        {
                //            ApplicationName = new Uri($"fabric:/{Constants.CaptainHookApplication.ApplicationName}"),
                //            HasPersistedState = true,
                //            MinReplicaSetSize = 3,
                //            TargetReplicaSetSize = 3,
                //            PartitionSchemeDescription = new SingletonPartitionSchemeDescription(),
                //            ServiceTypeName = Constants.CaptainHookApplication.EventReaderServiceType,
                //            ServiceName = new Uri(dispatcherServiceNameUri),
                //            InitializationData = Encoding.UTF8.GetBytes(host)
                //        });
                //}
            }
            catch (Exception ex)
            {
                BigBrother.Publish(ex.ToExceptionEvent());
                throw;
            }
        }
    }
}
