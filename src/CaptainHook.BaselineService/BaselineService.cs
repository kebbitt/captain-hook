using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace CaptainHook.BaselineService
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class BaselineService : StatelessService
    {
        private readonly IBigBrother _bb;
        private readonly ConfigurationSettings _settings;

        public BaselineService(StatelessServiceContext context, IBigBrother bb, ConfigurationSettings settings)
            : base(context)
        {
            _bb = bb;
            _settings = settings;
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await ServiceBusNamespaceExtensions.SetupHookTopic(
                _settings.AzureSubscriptionId,
                _settings.ServiceBusNamespace,
                TypeExtensions.GetEntityName(typeof(BaselineMessage).FullName));
        }
    }
}
