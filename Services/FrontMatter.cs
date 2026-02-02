using System.Globalization;

namespace downpatch.Services
{
    internal static class FrontMatter
    {
        public static (Dictionary<string, string> map, string body) TryParse(string raw)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!raw.StartsWith("---\n") && !raw.StartsWith("---\r\n"))
                return (map, raw);

            var end = raw.IndexOf("\n---", 4, StringComparison.Ordinal);
            if (end < 0)
                return (map, raw);

            var fmBlock = raw.Substring(4, end - 4).Replace("\r\n", "\n").Trim();
            var rest = raw[(end + 4)..]; // skip "\n---"
            rest = rest.TrimStart('\r', '\n');

            string? currentKey = null;
            var currentList = new List<string>();

            void FlushListIfAny()
            {
                if (currentKey is null) return;
                if (currentList.Count == 0) return;

                // store as newline-separated so GetList() can split later
                map[currentKey] = string.Join('\n', currentList);
                currentList.Clear();
                currentKey = null;
            }

            foreach (var line in fmBlock.Split('\n'))
            {
                var s = line.TrimEnd();

                if (string.IsNullOrWhiteSpace(s) || s.TrimStart().StartsWith('#'))
                    continue;

                var trimmed = s.TrimStart();

                // list item line?
                if (currentKey is not null && trimmed.StartsWith("- "))
                {
                    var item = trimmed[2..].Trim();
                    if (item.Length > 0)
                    {
                        // strip quotes
                        if (item.Length >= 2 && ((item[0] == '"' && item[^1] == '"') || (item[0] == '\'' && item[^1] == '\'')))
                            item = item[1..^1];

                        currentList.Add(item);
                    }
                    continue;
                }

                // new key line; flush any previous list
                FlushListIfAny();

                var idx = trimmed.IndexOf(':');
                if (idx <= 0) continue;

                var key = trimmed[..idx].Trim();
                var val = trimmed[(idx + 1)..].Trim();

                if (key.Length == 0) continue;

                // key:   (start list)
                if (val.Length == 0)
                {
                    currentKey = key;
                    continue;
                }

                // strip quotes
                if (val.Length >= 2 && ((val[0] == '"' && val[^1] == '"') || (val[0] == '\'' && val[^1] == '\'')))
                    val = val[1..^1];

                map[key] = val;
            }

            // flush list at EOF
            FlushListIfAny();

            return (map, rest);
        }


        public static bool GetBool(this IReadOnlyDictionary<string, string> map, string key, bool defaultValue = false)
        {
            if (!map.TryGetValue(key, out var s)) return defaultValue;

            if (bool.TryParse(s, out var b)) return b;

            // allow yes/no, 1/0
            s = s.Trim().ToLowerInvariant();
            if (s is "yes" or "y" or "1" or "true") return true;
            if (s is "no" or "n" or "0" or "false") return false;

            return defaultValue;
        }
        public static IReadOnlyList<string> GetList(this IReadOnlyDictionary<string, string> map, string key)
        {
            if (!map.TryGetValue(key, out var s) || string.IsNullOrWhiteSpace(s))
                return Array.Empty<string>();

            // Stored as newline-separated values internally
            return s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        public static string? GetString(this IReadOnlyDictionary<string, string> map, string key)
            => map.TryGetValue(key, out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

        public static DateTime? GetDateUtc(this IReadOnlyDictionary<string, string> map, string key)
        {
            if (!map.TryGetValue(key, out var s)) return null;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                return dt;
            return null;
        }
    }

}
