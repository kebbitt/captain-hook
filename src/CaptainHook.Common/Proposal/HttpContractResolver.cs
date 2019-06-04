using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CaptainHook.Common.Proposal
{
    public class HttpContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (property.AttributeProvider.GetAttributes(true).OfType<JsonIgnoreAttribute>().Any() ||
                property.AttributeProvider.GetAttributes(true).OfType<HttpIgnoreAttribute>().Any())
            {
                property.ShouldSerialize = _ => false;
            }

            return property;
        }
    }
}
