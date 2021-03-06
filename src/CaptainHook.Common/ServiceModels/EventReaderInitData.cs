﻿using System.IO;
using CaptainHook.Common.Configuration;
using Newtonsoft.Json;

namespace CaptainHook.Common.ServiceModels
{
    public class EventReaderInitData
    {
        public string EventType { get; set; }
        public string SubscriberName { get; set; }
        public SubscriberDlqMode? DlqMode { get; set; }
        /// <summary>
        /// source subscription for DLQ receiver
        /// </summary>
        public string SourceSubscription { get; set; }

        public string SubscriptionName => DlqMode!=null ? SourceSubscription : SubscriberName;

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
                        DlqMode = sub.DLQMode,
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
