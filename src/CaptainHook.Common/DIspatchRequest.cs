using System.Runtime.Serialization;
using CaptainHook.Common.Authentication;
using CaptainHook.Common.Configuration;

namespace CaptainHook.Common
{
    [DataContract]
    [KnownType(typeof(OidcAuthenticationConfig))]
    [KnownType(typeof(BasicAuthenticationConfig))]
    public class DispatchRequest
    {
        [DataMember]
        public string Uri { get; set; }

        [DataMember]
        public HttpVerb Verb { get; set; }

        [DataMember]
        public string Payload { get; set; }

        [DataMember]
        public AuthenticationConfig AuthenticationConfig { get; set; }

    }
}
