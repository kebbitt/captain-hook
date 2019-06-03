using System.ComponentModel;

namespace CaptainHook.Api.Proposal
{
    /// <summary>
    /// Contains application exit codes for Service Fabric components that should exit on critical failure scenarios.
    /// </summary>
    public static class ExitCode
    {
        /// <summary>
        /// Contains a group of exit codes for Cosmos related critical failures.
        /// </summary>
        public static class Cosmos
        {
            /// <summary>
            /// When the application fails to create the Cosmos Database.
            /// </summary>
            [Description("Failed to create the Cosmos Database")]
            public const int DatabaseCreationFailure = 10100;

            /// <summary>
            /// When the application fails to create the Cosmos Container in the Cosmos Database.
            /// </summary>
            [Description("Failed to create the Cosmos Container")]
            public const int ContainerCreationFailure = 10200;
        }
    }
}
