using System.ComponentModel;

namespace CaptainHook.Api.Proposal
{
    public static class ExitCode
    {
        public static class Cosmos
        {
            [Description("Failed to create the Cosmos Database")]
            public const int DatabaseCreationFailure = 10100;

            [Description("Failed to create the Cosmos Collection")]
            public const int ContainerCreationFailure = 10200;
        }
    }
}
