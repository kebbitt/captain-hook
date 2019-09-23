namespace CaptainHook.Common
{
    public struct Constants
    {
        public struct Headers
        {
            public const string CorrelationId = "X-Correlation-ID";
        }

        public struct CaptainHookApplication
        {
            public const string ApplicationName = "CaptainHook";

            public static readonly string ApplicationFabricUri = $"fabric:/{ApplicationName}";

            public struct DefaultServiceConfig
            {
                public const string DefaultMinReplicaSetSize = "DefaultMinReplicaSetSize";

                public const string TargetReplicaSetSize = "DefaultTargetReplicaSetSize";

                public const string DefaultPartitionCount = "DefaultPartitionCount";

                public const string DefaultPlacementConstraints = "DefaultPlacementConstraints";
            }

            public struct Services
            {
                public const string EventReaderServiceShortName = "EventReader";

                public static readonly string EventReaderServiceFullName = $"fabric:/{ApplicationName}/{EventReaderServiceShortName}";

                public const string EventReaderServiceType = "CaptainHook.EventReaderServiceType";

                public const string DirectorServiceShortName = "Director";

                public const string DirectorServiceType = "CaptainHook.DirectorServiceType";

                public const string EventDispatcherServiceName = "EventDispatcher";

                public const string EventDispatcherServiceType = "CaptainHook.EventDispatcherServiceType";

                public static readonly string EventDispatcherServiceFullName = $"fabric:/{ApplicationName}/{EventDispatcherServiceName}";

                public const string EventHandlerServiceShortName = "EventHandler";

                public static readonly string EventHandlerServiceFullName = $"fabric:/{ApplicationName}/{EventHandlerServiceShortName}";

                public const string EventHandlerActorServiceType = "EventHandlerActorServiceType";

            }
        }
    }
}
