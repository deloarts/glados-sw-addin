using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace GladosSwAddin.Api
{
    /// <summary>
    /// Builds "?a=b&amp;c=d"-style query strings from filter values, skipping nulls.
    /// </summary>
    internal static class QueryStringBuilder
    {
        public static string Build(IEnumerable<KeyValuePair<string, object>> values)
        {
            var sb = new StringBuilder();

            foreach (var kvp in values)
            {
                if (kvp.Value == null)
                    continue;

                var stringValue = ToQueryValue(kvp.Value);
                if (stringValue == null)
                    continue;

                sb.Append(sb.Length == 0 ? '?' : '&');
                sb.Append(Uri.EscapeDataString(kvp.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(stringValue));
            }

            return sb.ToString();
        }

        private static string ToQueryValue(object value)
        {
            switch (value)
            {
                case bool b:
                    return b ? "true" : "false";
                case DateTime dt:
                    return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                case Enum e:
                    return EnumWireValue(e);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Resolves the wire value of an enum member, honoring [EnumMember(Value=...)]
        /// so query params match the backend's exact string (e.g. "active", not "Active").
        /// </summary>
        private static string EnumWireValue(Enum value)
        {
            var member = value.GetType().GetField(value.ToString());
            var attribute = member?.GetCustomAttribute<EnumMemberAttribute>();
            return attribute?.Value ?? value.ToString();
        }
    }
}
