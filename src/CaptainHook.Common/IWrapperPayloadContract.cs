using System.Net;
using Newtonsoft.Json.Linq;

namespace CaptainHook.Common
{
    public interface IWrapperPayloadContract
    {
        JObject Payload { get; set; }
        string MessageId { get; set; }
        string EventType { get; set; }
        HttpStatusCode? StatusCode { get; set; }
        string StatusUri { get; set; }
        CallbackTypeEnum CallbackType { get; set; }
    }

    public enum CallbackTypeEnum
    {
        PartialFailure,
        CompleteFailure
    }
}
