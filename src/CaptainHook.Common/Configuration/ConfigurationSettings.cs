﻿namespace CaptainHook.Common.Configuration
{
    public class ConfigurationSettings
    {
        public const string KeyVaultUriEnvVariable = "KEYVAULT_BASE_URI";

        public string AzureSubscriptionId { get; set; }

        public string CosmosConnectionString { get; set; }

        public string InstrumentationKey { get; set; }

        public string ServiceBusConnectionString { get; set; }

        public string ServiceBusNamespace { get; set; }
    }
}
