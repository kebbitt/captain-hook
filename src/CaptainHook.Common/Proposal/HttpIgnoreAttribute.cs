using System;

namespace CaptainHook.Common.Proposal

{
    /// <summary>
    /// Marks a property to be ignored by the MVC serializer both on request and response payloads.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class HttpIgnoreAttribute : Attribute
    {
    }
}
