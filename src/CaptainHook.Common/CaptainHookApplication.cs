namespace CaptainHook.Common
{
    public struct Constants
    {
        public struct CaptainHookApplication
        {
            public const string ApplicationName = "CaptainHook";

            public struct Services
            {
                public const string EventReaderServicePrefix = "EventReader";

                public const string EventReaderServiceType = "CaptainHook.EventReaderServiceType";

                public const string DirectorServiceType = "CaptainHook.DirectorServiceType";

                public const string EventDispatcherServicePrefix = "Dispatcher";

                public const string EventDispatcherServiceType = "CaptainHook.EventDispatcherServiceType";

                public const string EventHandlerServicePrefix = "EventHandler";

                public const string EventHandlerServiceType = "CaptainHook.EventHandlerActorServiceType";
            }
        }
    }
}