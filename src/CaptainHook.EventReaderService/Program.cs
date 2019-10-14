using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using CaptainHook.Common;
using CaptainHook.Common.Configuration;
using Eshopworld.Core;
using Eshopworld.Telemetry;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using Microsoft.ServiceFabric.Actors.Client;

namespace CaptainHook.EventReaderService
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static async Task Main()
        {
            try
            {
                var kvUri = Environment.GetEnvironmentVariable(PlatformConfigurationSettings.KeyVaultUriEnvVariable);

                var config = new ConfigurationBuilder().AddAzureKeyVault(
                    kvUri,
                    new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback)),
                    new DefaultKeyVaultSecretManager()).Build();

                var settings = new PlatformConfigurationSettings();
                config.Bind(settings);

                var bigBrother = new BigBrother(settings.InstrumentationKey, settings.InstrumentationKey);
                bigBrother.UseEventSourceSink().ForExceptions();

                var builder = new ContainerBuilder();
                builder.RegisterInstance(bigBrother).As<IBigBrother>().SingleInstance();
                builder.RegisterInstance(settings).SingleInstance();
                builder.RegisterType<MessageProviderFactory>().As<IMessageProviderFactory>().SingleInstance();
                builder.RegisterType<ServiceBusManager>().As<IServiceBusManager>();

                //SF Deps
                builder.Register<IActorProxyFactory>(_ => new ActorProxyFactory());
                builder.RegisterServiceFabricSupport();
                builder.RegisterStatefulService<EventReaderService>(Constants.CaptainHookApplication.Services.EventReaderServiceType);

                using (builder.Build())
                {
                    ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, Constants.CaptainHookApplication.Services.EventReaderServiceType);
                    await Task.Delay(Timeout.Infinite);
                }
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                BigBrother.Write(e);
                throw;
            }
        }
    }
}
