using System;

namespace CaptainHook.Common
{
    public class MessageDataHandle
    {
        public Guid Handle { get; set; }

        public int HandlerId { get; set; }

        public string LockToken { get; set; }
    }
}