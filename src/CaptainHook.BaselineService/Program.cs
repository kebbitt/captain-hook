using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using CaptainHook.Common;
using Eshopworld.Core;
using Eshopworld.Telemetry;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;

namespace CaptainHook.BaselineService
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        public static async Task Main()
        {
            try
            {
                var kvUri = Environment.GetEnvironmentVariable(ConfigurationSettings.KeyVaultUriEnvVariable);

                var config = new ConfigurationBuilder().AddAzureKeyVault(
                    kvUri,
                    new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback)),
                    new DefaultKeyVaultSecretManager()).Build();

                var settings = new ConfigurationSettings();
                config.Bind(settings);

                var bb = new BigBrother(settings.InstrumentationKey, settings.InstrumentationKey);
                bb.UseEventSourceSink().ForExceptions();

                var builder = new ContainerBuilder();
                builder.RegisterInstance(bb)
                       .As<IBigBrother>()
                       .SingleInstance();

                builder.RegisterInstance(settings)
                       .SingleInstance();


                builder.RegisterServiceFabricSupport();
                builder.RegisterStatelessService<BaselineService>("DemoStatelessServiceType");

                using (builder.Build())
                {
                    await Task.Delay(Timeout.Infinite);
                }
            }
            catch (Exception e)
            {
                BigBrother.Write(e);
            }
        }
    }
}