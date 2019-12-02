using Autofac;
using Eshopworld.Telemetry;
using Eshopworld.Telemetry.Configuration;

namespace CaptainHook.Common.Telemetry
{
    public static class TelemetryExtensions
    {
        public static void SetupFullTelemetry(this ContainerBuilder builder)
        {
            builder.ConfigureTelemetryKeys("d9bda9b7-afd9-4b88-80c7-c6a9f918e682", "d9bda9b7-afd9-4b88-80c7-c6a9f918e682");
            builder.AddStatefullServiceTelemetry();

            builder.RegisterModule<TelemetryModule>();
            builder.RegisterModule<ServiceFabricTelemetryModule>();
        }
    }
}
