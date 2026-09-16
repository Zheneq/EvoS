using System.Text.RegularExpressions;

namespace EvoS.Framework.Misc
{
    public static partial class LogRedaction
    {
        public static string MaskSensitiveJsonFields(string json)
        {
            return string.IsNullOrEmpty(json)
                ? json
                : SensitiveJsonFields().Replace(json, "$1\"***\"");
        }

        [GeneratedRegex("(\"(?:Password|TicketData)\"\\s*:\\s*)\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex SensitiveJsonFields();
    }
}
