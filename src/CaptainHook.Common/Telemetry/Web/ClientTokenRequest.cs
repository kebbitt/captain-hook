using Eshopworld.Core;

namespace CaptainHook.Common.Telemetry.Web
{
    public class ClientTokenRequest : TelemetryEvent
    {
        public string ClientId { get; set; }

        public string Authority { get; set; }

        public string Message { get; set; }
    }
}
