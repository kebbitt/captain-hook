namespace CaptainHook.Common
{
    public struct Constants
    {
        public struct CaptainHookApplication
        {
            public const string ApplicationName = "CaptainHook";

            public struct DefaultServiceConfig
            {
                public const string DefaultMinReplicaSetSize = "DefaultMinReplicaSetSize";

                public const string TargetReplicaSetSize = "DefaultTargetReplicaSetSize";

                public const string DefaultPartitionCount = "DefaultPartitionCount";
            }

            public struct Services
            {
                public const string EventReaderServiceName = "EventReader";

                public const string EventReaderServiceType = "CaptainHook.EventReaderServiceType";

                public const string DirectorServiceType = "CaptainHook.DirectorServiceType";

                public const string EventDispatcherServiceName = "Dispatcher";

                public const string EventDispatcherServiceType = "CaptainHook.EventDispatcherServiceType";

                public const string EventHandlerServiceName = "EventHandler";

                public const string EventHandlerServiceType = "CaptainHook.EventHandlerActorServiceType";
            }
        }
    }
}