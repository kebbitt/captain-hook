using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace CaptainHook.Common.Exceptions
{
    [DataContract]
    [Serializable]
    public class NoLongerPrimaryReplicaException : Exception
    {
        public NoLongerPrimaryReplicaException():base("I am no longer primary replica")
        {

        }

        public NoLongerPrimaryReplicaException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
