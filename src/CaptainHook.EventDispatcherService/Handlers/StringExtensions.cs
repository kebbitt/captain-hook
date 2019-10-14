using System;

namespace CaptainHook.EventDispatcherService.Handlers
{
    public static class StringExtensions
    {

        public static int LastIndexOfSafe(this string value, char searchCharacter)
        {
            try
            {
                var position = value.LastIndexOf("/", StringComparison.Ordinal);
                return position;
            }
            catch
            {
                // ignored
            }

            return 0;
        }
    }
}
