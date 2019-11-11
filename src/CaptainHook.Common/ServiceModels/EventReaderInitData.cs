using System.IO;
using CaptainHook.Common.Configuration;
using Newtonsoft.Json;

namespace CaptainHook.Common.ServiceModels
{
    public class EventReaderInitData
    {
        public string EventType { get; set; }
        public string SubscriberName { get; set; }
        public SubscriberDLQMode? DLQMode { get; set; }
        /// <summary>
        /// source subscription for DQL receiver
        /// </summary>
        public string SourceSubscription { get; set; }

        public string SubscriptionName => DLQMode!=null ? SourceSubscription : SubscriberName;

        public static string GetReaderInitDataAsString(string eventType, string subName)            
        {
            return GetReaderInitDataAsString(new SubscriberConfiguration { EventType = eventType, SubscriberName = subName });
        }

        public static string GetReaderInitDataAsString(SubscriberConfiguration sub)
        {
            using (var sw = new StringWriter())
            {
                using (var writer = new JsonTextWriter(sw))
                {
                    var inst = new EventReaderInitData
                    {
                        SubscriberName = sub.SubscriberName,
                        EventType = sub.EventType,
                        DLQMode = sub.DLQMode,
                        SourceSubscription = sub.DLQMode!=null? sub.SourceSubscriptionName : null
                    };

                    JsonSerializer.CreateDefault().Serialize(writer, inst);

                    writer.Flush();
                    return sw.ToString();
                }
            }
        }
    }
}
