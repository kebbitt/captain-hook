using CaptainHook.Common.Configuration;

namespace CaptainHook.DirectorService
{
    public sealed class Configuration
    {
        public DefaultServiceSettings DefaultServiceSettings { get; set; }

        public DispatcherConfigType DispatcherConfig { get;  set; }

        public class DispatcherConfigType
        {
            public int PoolSize { get; set; }
        }

    }
}
