using System;
using System.Collections.Generic;
using System.Net.Http;
using CaptainHook.Common.Authentication;

namespace CaptainHook.Common.Configuration
{
    /// <summary>
    /// Webhook config contains details for the webhook, eg uri and auth details
    /// </summary>
    public class WebhookConfig
    {
        public WebhookConfig()
        {
            AuthenticationConfig = new AuthenticationConfig();
            WebhookRequestRules = new List<WebhookRequestRule>();
        }

        /// <summary>
        /// 
        /// </summary>
        public AuthenticationConfig AuthenticationConfig { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public HttpMethod HttpMethod { get; set; } = HttpMethod.Post;

        /// <summary>
        /// The event type of this event
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// The default http content type used for events
        /// </summary>
        public string ContentType { get; set; } = Constants.Headers.DefaultContentType;

        /// <summary>
        /// 
        /// </summary>
        public List<WebhookRequestRule> WebhookRequestRules { get; set; }

        /// <summary>
        /// Request duration maximum timeout in seconds
        /// Left at 100 seconds as the default value for the http client timeout
        /// </summary>
        public TimeSpan Timeout { get; set; } = new TimeSpan(0, 0, 100);
    }

    /// <summary>
    /// Defines the configuration of the subscriber of a topic.
    /// </summary>
    public class SubscriberConfiguration : WebhookConfig
    {
        /// <summary>
        /// Creates a key used to index the subscriber configurations.
        /// </summary>
        /// <param name="typeName">The full name of the type of the message.</param>
        /// <param name="subscriberName">The optional name of the subsciber.</param>
        /// <returns></returns>
        public static string Key(string typeName, string subscriberName)
            => $"{typeName};{subscriberName}".ToLowerInvariant();

        /// <summary>
        /// The configuration of the webhook which should receive the response message from
        /// the main webhook.
        /// </summary>
        public WebhookConfig Callback { get; set; }

        /// <summary>
        /// intended name for the subscriber(and therefore subscription)
        /// </summary>
        public string SubscriberName { get; set; }

        /// <summary>
        /// Specifies a configuration which should be used when the name has not been provided.
        /// </summary>
        /// <remarks>
        /// It's for backward compatiblity only.
        /// </remarks>
        public bool IsMainConfiguration { get; private set; }

        /// <summary>
        /// Tranforms the old structure of configuration into the new one.
        /// </summary>
        /// <param name="webhookConfig">The webhook configuration.</param>
        /// <param name="callback">The callback associated with <paramref name="webhookConfig"/>.</param>
        /// <returns>The subscriber configuration consisting of the webhook configuration and its callback.</returns>
        public static SubscriberConfiguration FromWebhookConfig(WebhookConfig webhookConfig, WebhookConfig callback)
        {
            return new SubscriberConfiguration
            {
                AuthenticationConfig = webhookConfig.AuthenticationConfig,
                HttpMethod = webhookConfig.HttpMethod,
                //EventType = webhookConfig.EventType,
                ContentType = webhookConfig.ContentType,
                Name = webhookConfig.Name,
                SubscriberName = "captain-hook", //for the legacy config, assume the legacy name as well
                Timeout = webhookConfig.Timeout,
                Uri = webhookConfig.Uri,
                WebhookRequestRules = webhookConfig.WebhookRequestRules,
                Callback = callback,
                IsMainConfiguration = true,
            };
        }
    }

    /// <summary>
    /// Event handler config contains both details for the webhook call as well as any domain events and callback
    /// </summary>
    public class EventHandlerConfig
    {
        /// <summary>
        /// The list of all subscibers of the topic handling the event type.
        /// </summary>
        public List<SubscriberConfiguration> Subscribers { get; } = new List<SubscriberConfiguration>();

        /// <summary>
        /// Returns all subscribers defined in the old and new configuration schemas.
        /// </summary>
        public IEnumerable<SubscriberConfiguration> AllSubscribers
        {
            get
            {
                if (WebhookConfig != null)
                    yield return SubscriberConfiguration.FromWebhookConfig(WebhookConfig, CallbackConfig);
                foreach (var conf in Subscribers)
                    yield return conf;
            }
        }

        /// <summary>
        /// The webhook definition using the old schema of configuration.
        /// </summary>
        public WebhookConfig WebhookConfig { get; set; }

        /// <summary>
        /// The callback of associated with <see cref="WebhookConfig"/> using the old schema of configuration.
        /// </summary>
        public WebhookConfig CallbackConfig { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }
    }

    public class WebhookRequestRule
    {
        public WebhookRequestRule()
        {
            Routes = new List<WebhookConfigRoute>();
            Source = new ParserLocation();
            Destination = new ParserLocation();
        }

        /// <summary>
        /// ie from payload, header, etc etc
        /// </summary>
        public ParserLocation Source { get; set; }

        /// <summary>
        /// ie uri, body, header
        /// </summary>
        public ParserLocation Destination { get; set; }

        /// <summary>
        /// Routes used for webhook rule types
        /// </summary>
        public List<WebhookConfigRoute> Routes { get; set; }
    }

    public class WebhookConfigRoute : WebhookConfig
    {
        /// <summary>
        /// A selector that is used in the payload to determine where the request should be routed to in the config
        /// </summary>
        public string Selector { get; set; }
    }

    public class ParserLocation
    {
        /// <summary>
        /// Path for the parameter to query from or to be placed
        /// ie: path in the message both or if it's a value in the http header
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The location of the parsed parameter or the location it should go
        /// </summary>
        public Location Location { get; set; } = Location.Body;

        /// <summary>
        /// 
        /// </summary>
        public RuleAction RuleAction { get; set; } = RuleAction.Add;

        /// <summary>
        /// The data type of the property
        /// </summary>
        public DataType Type { get; set; } = DataType.Property;
    }

    public enum DataType
    {
        Property = 1,
        HttpContent = 2,
        HttpStatusCode = 3,
        Model = 4,
        String = 5
    }

    public enum RuleAction
    {
        Replace = 1,
        Add = 2,
        Route = 3
    }

    public enum Location
    {
        /// <summary>
        /// Mostly used to add something to the URI of the request
        /// </summary>
        Uri = 1,

        /// <summary>
        /// The request payload body. Can come from or be attached to
        /// </summary>
        Body = 2,

        /// <summary>
        /// 
        /// </summary>
        Header = 3,

        /// <summary>
        /// Special case to get the status code of the webhook request and add it to the call back body
        /// </summary>
        HttpStatusCode = 4,

        /// <summary>
        /// Special case to get the status code of the webhook request and add it to the call back body
        /// </summary>
        HttpContent = 5,
    }
}
