using System;
using System.IO;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Web;
using Eshopworld.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using System.Reflection;
using CaptainHook.Api.Controllers;
using CaptainHook.Api.Proposal;
using CaptainHook.Common;
using CaptainHook.Common.Proposal;
using CaptainHook.Common.Rules;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Azure.Cosmos;

namespace CaptainHook.Api
{
    /// <summary>
    /// Startup class for ASP.NET runtime
    /// </summary>
    public class Startup
    {
        private readonly TelemetrySettings _telemetrySettings = new TelemetrySettings();
        private readonly IBigBrother _bb;
        private readonly IConfigurationRoot _configuration;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="env">hosting environment</param>
        public Startup(IHostingEnvironment env)
        {
            try
            {
                _configuration = EswDevOpsSdk.BuildConfiguration(env.ContentRootPath, env.EnvironmentName);
                _configuration.GetSection("Telemetry").Bind(_telemetrySettings);
                _bb = new BigBrother(_telemetrySettings.InstrumentationKey, _telemetrySettings.InternalKey);
            }
            catch (Exception e)
            {
                BigBrother.Write(e);
                throw;
            }
        }

        /// <summary>
        /// configure services to be used by the asp.net runtime
        /// </summary>
        /// <param name="services">service collection</param>
        /// <returns>service provider instance (Autofac provider)</returns>
        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                services.AddApplicationInsightsTelemetry(_telemetrySettings.InstrumentationKey);
                services.Configure<ServiceConfigurationOptions>(_configuration.GetSection("ServiceConfigurationOptions"));

                services.AddMvc()
                        .AddJsonOptions(options => options.SerializerSettings.ContractResolver = new HttpContractResolver());

                services.AddApiVersioning();

                //Get XML documentation
                var path = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");

                //if not generated throw an event but it's not going to stop the app from starting
                if (!File.Exists(path))
                {
                    BigBrother.Write(new InvalidOperationException("Swagger XML document has not been included in the project"));
                }
                else
                {
                    services.AddSwaggerGen(c =>
                    {
                        c.IncludeXmlComments(path);
                        c.DescribeAllEnumsAsStrings();
                        c.SwaggerDoc("v1", new Info { Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(), Title = Assembly.GetExecutingAssembly().GetName().Name });
                        c.CustomSchemaIds(x => x.FullName);
                    });
                }
            }
            catch (Exception e)
            {
                _bb.Publish(e.ToExceptionEvent());
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        public void ConfigureContainer(ContainerBuilder builder)
        {
            try
            {
                builder.RegisterInstance(_bb).As<IBigBrother>().SingleInstance();

                var configurationSettings = new ConfigurationSettings();
                _configuration.Bind(configurationSettings);
                builder.RegisterInstance(configurationSettings);

                var cosmosClient = new CosmosClient(configurationSettings.CosmosConnectionString);
                builder.RegisterInstance(cosmosClient);

                var database = cosmosClient.Databases.CreateDatabaseIfNotExistsAsync("captain-hook", 400).Result.HandleResponse(_bb);
                builder.RegisterInstance(database);

                var ruleContainer = database.Containers.CreateContainerIfNotExistsAsync(nameof(RoutingRule), RoutingRule.PartitionKeyPath).Result.HandleResponse(_bb);
                builder.RegisterInstance(ruleContainer).Keyed<CosmosContainer>(nameof(RoutingRule)).SingleInstance();

                var ruleSetContainer = database.Containers.CreateContainerIfNotExistsAsync(nameof(RoutingRuleSet), RoutingRuleSet.PartitionKeyPath).Result.HandleResponse(_bb);
                builder.RegisterInstance(ruleSetContainer).Keyed<CosmosContainer>(nameof(RoutingRuleSet)).SingleInstance();
            }
            catch (Exception e)
            {
                _bb.Publish(e.ToExceptionEvent());
                throw;
            }
        }

        /// <summary>
        /// configure asp.net pipeline
        /// </summary>
        /// <param name="app">application builder</param>
        public void Configure(IApplicationBuilder app)
        {
            app.UseBigBrotherExceptionHandler();
            app.UseSwagger(o => o.RouteTemplate = "swagger/{documentName}/swagger.json");
            app.UseSwaggerUI(o =>
            {
                o.SwaggerEndpoint("v1/swagger.json", "CaptainHook.Api");
                o.RoutePrefix = "swagger";
            });

            app.UseAuthentication();

            app.UseMvc();
        }
    }
}
