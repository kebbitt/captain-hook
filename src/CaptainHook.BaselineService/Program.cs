using System;
using System.Threading;
using Autofac;
using Autofac.Integration.ServiceFabric;

namespace CaptainHook.BaselineService
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            try
            {
                var builder = new ContainerBuilder();

                builder.RegisterServiceFabricSupport();
                builder.RegisterStatelessService<BaselineService>("DemoStatelessServiceType");

                using (builder.Build())
                {
                    // Prevents this host process from terminating so services keep running.
                    Thread.Sleep(Timeout.Infinite);
                }
            }
            catch (Exception e)
            {
            }
        }
    }
}
