using System;

namespace CaptainHook.Common
{
    public class BaselineMessage
    {
        public BaselineMessage()
        {
            RandomGuid = Guid.NewGuid();
        }

        public Guid RandomGuid { get; }
    }
}
