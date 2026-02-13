using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Downpatch.Web.Services
{
    public sealed class NavStore
    {
        private readonly string _contentRoot;
        private readonly Dictionary<string, IReadOnlyList<NavNode>> _tocByFolder = new(StringComparer.OrdinalIgnoreCase);

        private static readonly IDeserializer _yaml = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        public NavStore(string contentRoot) => _contentRoot = contentRoot;

        public void Build()
        {
            _tocByFolder.Clear();

            if (!Directory.Exists(_contentRoot))
                return;

            foreach (var tocPath in Directory.EnumerateFiles(_contentRoot, "toc.yml", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(tocPath);
                if (!full.StartsWith(_contentRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    continue;

                var folderRel = Path.GetDirectoryName(full)!;
                folderRel = folderRel.Substring(_contentRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/')
                    .Trim('/'); // e.g. "guide/halo/haloreach"

                try
                {
                    var yamlText = File.ReadAllText(full);

                    var raw = _yaml.Deserialize<List<NavNodeYaml>>(yamlText) ?? new();
                    var resolved = raw.Select(n => ResolveNode(n, "/" + folderRel)).ToList();

                    _tocByFolder[folderRel] = resolved;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"toc.yml parse failed: {full} :: {ex.Message}");
                }

            }
        }

        public IReadOnlyList<NavNode> GetNavForRequestSlug(string? slug)
        {
            var folder = NormalizeToFolderKey(slug);

            while (true)
            {
                if (_tocByFolder.TryGetValue(folder, out var toc))
                    return toc;

                var lastSlash = folder.LastIndexOf('/');
                if (lastSlash < 0) break;

                folder = folder[..lastSlash];
            }

            return Array.Empty<NavNode>();
        }

        private static string NormalizeToFolderKey(string? slug)
        {
            slug ??= "";
            slug = slug.Replace('\\', '/').Trim('/');

            if (slug.Length == 0)
                return "guide";

            return $"guide/{slug}".Trim('/');
        }

        private static NavNode ResolveNode(NavNodeYaml y, string basePath)
        {
            var href = y.Href is null ? null : NormalizeHref(basePath, y.Href);
            var items = (y.Items ?? new()).Select(i => ResolveNode(i, basePath)).ToList();
            return new NavNode(y.Name ?? "Untitled", href, items);
        }

        private static string NormalizeHref(string basePath, string href)
        {
            href = href.Replace('\\', '/').Trim();

            if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return href;

            if (href.StartsWith("/"))
                return StripMdAndIndex(href);

            return StripMdAndIndex($"{basePath}/{href}");
        }

        private static string StripMdAndIndex(string path)
        {
            path = path.Replace('\\', '/');

            if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                path = path[..^3];

            if (path.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
                path = path[..^("/index".Length)];

            return path;
        }

        private sealed class NavNodeYaml
        {
            public string? Name { get; set; }
            public string? Href { get; set; }
            public List<NavNodeYaml>? Items { get; set; }
        }
    }

    public sealed record NavNode(string Name, string? Href, IReadOnlyList<NavNode> Items);
}
