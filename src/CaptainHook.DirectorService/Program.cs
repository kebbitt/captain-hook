using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using CaptainHook.Common;
using CaptainHook.Common.Rules;
using Eshopworld.Core;
using Eshopworld.Telemetry;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;

namespace CaptainHook.DirectorService
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
                var kvUri = Environment.GetEnvironmentVariable(ConfigurationSettings.KeyVaultUriEnvVariable);

                var config = new ConfigurationBuilder().AddAzureKeyVault(
                                                           kvUri,
                                                           new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback)),
                                                           new DefaultKeyVaultSecretManager())
                                                       .Build();

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

                builder.RegisterType<FabricClient>().SingleInstance();

                var cosmosClient = new CosmosClient(settings.CosmosConnectionString);
                builder.RegisterInstance(cosmosClient);

                var database = (await cosmosClient.Databases.CreateDatabaseIfNotExistsAsync("captain-hook", 400)).Database;
                builder.RegisterInstance(database);

                var ruleContainer = (await database.Containers.CreateContainerIfNotExistsAsync(nameof(RoutingRule), RoutingRule.PartitionKeyPath)).Container;
                builder.RegisterInstance(ruleContainer).SingleInstance();

                builder.RegisterServiceFabricSupport();
                builder.RegisterStatefulService<DirectorService>(CaptainHookApplication.DirectorServiceType);

                using (builder.Build()) { await Task.Delay(Timeout.Infinite); } // block
            }
            catch (Exception e)
            {
                BigBrother.Write(e);
            }
        }
    }
}
