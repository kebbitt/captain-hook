using System.Runtime.Serialization;

namespace CaptainHook.Common.Authentication
{
    [DataContract]
    public enum AuthenticationType
    {
        None = 0,
        [EnumMember]
        Basic = 1,
        [EnumMember]
        OIDC = 2,
        [EnumMember]
        Custom = 3
    }
}
