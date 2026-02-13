namespace Downpatch.Web.Services
{
    public sealed class ContentIndex
    {
        private readonly string _contentRoot;
        private readonly Dictionary<string, IndexEntry> _map = new(StringComparer.OrdinalIgnoreCase);

        public ContentIndex(string contentRoot) => _contentRoot = contentRoot;
        public IReadOnlyList<IndexEntry> AllEntries => _map.Values
    .DistinctBy(x => x.Slug)
    .ToList();
        public void Build()
        {
            _map.Clear();

            if (!Directory.Exists(_contentRoot))
                return;

            foreach (var file in Directory.EnumerateFiles(_contentRoot, "*.md", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(file);
                if (!full.StartsWith(_contentRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    continue;

                var slug = FilePathToSlug(full);
                var entry = new IndexEntry(slug, full, File.GetLastWriteTimeUtc(full));

                _map[slug] = entry;

                if (slug.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
                {
                    var folderSlug = slug[..^("/index".Length)];
                    if (!_map.ContainsKey(folderSlug))
                        _map[folderSlug] = entry;
                }
            }
        }

        public bool TryResolve(string? slug, out IndexEntry entry)
        {
            var key = NormalizeSlug(slug);
            return _map.TryGetValue(key, out entry);
        }

        private static string NormalizeSlug(string? slug)
        {
            slug ??= "";
            slug = slug.Replace('\\', '/').Trim('/');

            if (slug.Length == 0) return "guide/index";
            if (slug.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) slug = slug[..^3];

            return $"guide/{slug}".Trim('/');
        }

        private string FilePathToSlug(string fullPath)
        {
            var rel = fullPath.Substring(_contentRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');

            if (rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                rel = rel[..^3];

            return rel.Trim('/');
        }

        public readonly record struct IndexEntry(string Slug, string FilePath, DateTime LastModifiedUtc);
    }
}
