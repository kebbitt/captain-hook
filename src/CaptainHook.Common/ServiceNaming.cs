using static CaptainHook.Common.Constants;

namespace CaptainHook.Common
{
    public struct ServiceNaming
    {
        public const string EventReaderServiceShortName = "EventReader";

        public static string EventReaderServiceFullUri(string eventName, string despatchName) => $"fabric:/{CaptainHookApplication.ApplicationName}/{EventReaderServiceShortName}.{eventName}.{despatchName}";

        public const string EventReaderServiceType = "CaptainHook.EventReaderServiceType";

        public const string DirectorServiceShortName = "Director";

        public const string DirectorServiceType = "CaptainHook.DirectorServiceType";

        public const string EventDispatcherServiceName = "Dispatcher";

        public const string EventDispatcherServiceType = "CaptainHook.EventDispatcherServiceType";

        public const string EventHandlerServiceShortName = "EventHandler";

        public static readonly string EventHandlerServiceFullName = $"fabric:/{CaptainHookApplication.ApplicationName}/{EventHandlerServiceShortName}";

        public const string EventHandlerActorServiceType = "EventHandlerActorServiceType";

    }
}
