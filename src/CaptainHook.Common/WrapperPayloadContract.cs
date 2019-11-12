using System.Net;
using Newtonsoft.Json.Linq;

namespace CaptainHook.Common
{
    public class WrapperPayloadContract : IWrapperPayloadContract
    {
        public JObject Payload { get; set; }
        public string MessageId { get; set; }
        public string EventType { get; set; }
        public HttpStatusCode? StatusCode { get; set; }
        public string StatusUri { get; set; }
        public CallbackTypeEnum CallbackType { get; set; }
    }
}
