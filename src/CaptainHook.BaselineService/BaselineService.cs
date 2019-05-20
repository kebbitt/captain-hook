using System;
using System.Collections.Generic;
using System.Fabric;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CaptainHook.Common;
using Eshopworld.Core;
using Microsoft.Azure.ServiceBus;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;

namespace CaptainHook.BaselineService
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    public class BaselineService : StatelessService
    {
        private readonly IBigBrother _bb;
        private readonly ConfigurationSettings _settings;
        private TopicClient _sender;

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
            try
            {
                var topic = await ServiceBusNamespaceExtensions.SetupHookTopic(
                    _settings.AzureSubscriptionId,
                    _settings.ServiceBusNamespace,
                    TypeExtensions.GetEntityName(typeof(BaselineMessage).FullName));

                _sender = new TopicClient(
                    _settings.ServiceBusConnectionString,
                    topic.Name,
                    new RetryExponential(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), 3));

                while (!cancellationToken.IsCancellationRequested)
                {
                    await PumpMessages(cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _bb.Publish(ex.ToExceptionEvent());
                throw;
            }
        }

        private async Task PumpMessages(CancellationToken cancellationToken)
        {
            try
            {
                for (var i = 0; i < 5; i++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var qMessage = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new BaselineMessage())))
                    {
                        ContentType = "application/json",
                        Label = typeof(BaselineMessage).FullName
                    };

                    await _sender.SendAsync(qMessage);
                }
            }
            catch (Exception ex)
            {
                _bb.Publish(ex.ToExceptionEvent());
                // don't rethrow, because we don't want to fault the entire service instance for a single message pump action
            }
        }
    }
}
