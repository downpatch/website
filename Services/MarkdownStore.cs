using downpatch.Data;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace downpatch.Services
{
    public sealed class MarkdownStore
    {
        private readonly SlugMapper _mapper;
        private readonly MarkdownOptions _opt;

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

        public MarkdownStore(IWebHostEnvironment env, IOptions<MarkdownOptions> opt)
        {
            _opt = opt.Value;
            _mapper = new SlugMapper(env, opt);
        }

        public bool ContentRootExists()
            => Directory.Exists(_mapper.ContentRootPath);
        public IEnumerable<DocEntry> GetChildren(string? slug)
        {
            slug ??= "";
            slug = slug.Trim('/');

            var root = _mapper.ContentRootPath;

            var basePath = string.IsNullOrWhiteSpace(slug)
                ? root
                : Path.Combine(root, slug.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(basePath))
                yield break;

            foreach (var dir in Directory.EnumerateDirectories(basePath))
            {
                var index = Path.Combine(dir, _opt.DefaultDocument);
                if (!File.Exists(index)) continue;

                var childSlug = _mapper.SlugFromFilePath(index);
                yield return LoadEntry(childSlug, index);
            }

            foreach (var file in Directory.EnumerateFiles(basePath, "*.md"))
            {
                if (Path.GetFileName(file).Equals(_opt.DefaultDocument, StringComparison.OrdinalIgnoreCase))
                    continue;

                var childSlug = _mapper.SlugFromFilePath(file);
                yield return LoadEntry(childSlug, file);
            }
        }

        private DocEntry LoadEntry(string slug, string path)
        {
            var raw = File.ReadAllText(path, Encoding.UTF8);
            var (fm, _) = FrontMatter.TryParse(raw);

            var title = fm.GetString("nav_title") ?? fm.GetString("title") ?? GuessTitleFromSlug(slug);

            return new DocEntry
            {
                Slug = slug,
                Title = title,
                Description = fm.GetString("description"),
                Order = int.TryParse(fm.GetString("order"), out var o) ? o : 0,
                NavHide = fm.GetBool("nav_hide", false),
                IsIndex = Path.GetFileName(path).Equals(_opt.DefaultDocument, StringComparison.OrdinalIgnoreCase)
            };
        }
        public string GetNavRootSlug(string? currentSlug)
        {
            var slug = (currentSlug ?? "").Trim('/');

            while (true)
            {
                if (HasIndex(slug))
                    return slug;

                var lastSlash = slug.LastIndexOf('/');
                if (lastSlash < 0) break;

                slug = slug[..lastSlash];
            }

            return HasIndex("") ? "" : "";
        }

        private bool HasIndex(string slug)
        {
            var path = _mapper.MapSlugToPath(slug);
                                                    
            var root = _mapper.ContentRootPath;

            if (string.IsNullOrWhiteSpace(slug))
                return File.Exists(Path.Combine(root, _opt.DefaultDocument));

            var indexPath = Path.Combine(root, slug.Replace('/', Path.DirectorySeparatorChar), _opt.DefaultDocument);
            return File.Exists(indexPath);
        }

        public async Task<(MarkdownDoc? doc, FileInfo? file)> TryGetByRequestAsync(
            string? rawSlug,
            string host,
            CancellationToken ct = default)
        {
            var slug = _mapper.NormalizeSlug(rawSlug);

            var path = _mapper.MapSlugToPath(slug);
            if (!File.Exists(path))
                return (null, null);

            var fi = new FileInfo(path);
            var lastWrite = fi.LastWriteTimeUtc;

            if (_cache.TryGetValue(path, out var cached) && cached.LastWriteUtc == lastWrite)
                return (cached.Doc, fi);

            var raw = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
            var (fm, body) = FrontMatter.TryParse(raw);

            var title = fm.GetString("title") ?? GuessTitleFromSlug(slug);
            var description = fm.GetString("description");
            var canonical = fm.GetString("canonical");
            var noindex = fm.GetBool("noindex", defaultValue: false);
            var gameName = fm.GetString("game") ?? title;
            var leaderboardUrl = fm.GetString("leaderboard");
            var timingMethod = fm.GetString("timing_method"); // e.g. "RTA (AutoSplitter + LRT)" etc
            var downpatchRequired = fm.GetString("downpatch"); // "yes|no|sometimes"
            var allowedVersions = fm.GetString("allowed_versions");
            var whereToBuy = fm.GetString("where_to_buy");
            var platforms = fm.GetList("platforms");


            var ogImageRaw = fm.GetString("og_image") ?? fm.GetString("og:image");
            var squareImageRaw = fm.GetString("square_image") ?? fm.GetString("square:image");

            var ogImage = ResolveAssetPath(ogImageRaw, slug);
            var squareImage = ResolveAssetPath(squareImageRaw, slug);

            var etag = ComputeETag(fi.Length, lastWrite);

            var doc = new MarkdownDoc
            {
                Slug = slug,
                Title = title,
                Description = description,

                GameName = gameName,
                LeaderboardUrl = leaderboardUrl,
                TimingMethod = timingMethod,
                DownpatchRequired = downpatchRequired,
                AllowedVersions = allowedVersions,
                WhereToBuy = whereToBuy,
                Platforms = platforms,

                Canonical = canonical,
                NoIndex = noindex,
                Html = body,
                LastModifiedUtc = lastWrite,
                ETag = etag,
                OgImage = ogImage,
                SquareImage = squareImage
            };

            Console.WriteLine($"slug={slug} path={path} exists={File.Exists(path)}");

            _cache[path] = new CacheEntry(lastWrite, doc);
            return (doc, fi);
        }

        public IEnumerable<(string slug, DateTime lastModifiedUtc)> EnumerateAll()
        {
            if (!Directory.Exists(_mapper.ContentRootPath))
                yield break;

            foreach (var file in Directory.EnumerateFiles(_mapper.ContentRootPath, "*.md", SearchOption.AllDirectories))
            {
                var slug = _mapper.SlugFromFilePath(file);
                yield return (slug, File.GetLastWriteTimeUtc(file));
            }
        }

        private static string GuessTitleFromSlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return "Home";

            var last = slug.Split('/').Last();
            var words = last.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length == 0) return "Document";

            return string.Join(' ', words.Select(w =>
                w.Length == 1 ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..]));
        }

        private static string ComputeETag(long length, DateTime lastWriteUtc)
        {
            var s = $"{length}:{lastWriteUtc.Ticks}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            return $"\"{Convert.ToHexString(bytes)}\"";
        }

        public NavNode? BuildNavTree(string navRootSlug, string? currentSlug)
        {
            navRootSlug = (navRootSlug ?? "").Trim('/');
            currentSlug = (currentSlug ?? "").Trim('/');

            var rootPath = ResolveFolderPath(navRootSlug);
            if (rootPath is null) return null;

            var rootIndex = ResolveIndexPath(navRootSlug);
            var rootEntry = rootIndex is not null && File.Exists(rootIndex)
                ? LoadEntry(navRootSlug, rootIndex)
                : new DocEntry { Slug = navRootSlug, Title = string.IsNullOrWhiteSpace(navRootSlug) ? "Home" : GuessTitleFromSlug(navRootSlug), Description = null, Order = 0, NavHide = false, IsIndex = true };

            var rootNode = new NavNode
            {
                Slug = rootEntry.Slug,
                Title = rootEntry.Title,
                Description = rootEntry.Description,
                Order = rootEntry.Order,
                IsIndex = true
            };

            rootNode.Children = BuildChildrenRecursive(navRootSlug, currentSlug).ToList();

            // Mark current/ancestor flags
            MarkCurrent(rootNode, currentSlug);

            return rootNode;
        }

        private IEnumerable<NavNode> BuildChildrenRecursive(string parentSlug, string currentSlug)
        {
            var baseFolder = ResolveFolderPath(parentSlug);
            if (baseFolder is null || !Directory.Exists(baseFolder))
                yield break;

            // 1) child folders that have index.md
            foreach (var dir in Directory.EnumerateDirectories(baseFolder))
            {
                var index = Path.Combine(dir, _opt.DefaultDocument);
                var hasIndex = File.Exists(index);

                if (hasIndex)
                {
                    var childSlug = _mapper.SlugFromFilePath(index);
                    var entry = LoadEntry(childSlug, index);
                    if (!entry.NavHide)
                    {
                        var node = new NavNode
                        {
                            Slug = entry.Slug,
                            Title = entry.Title,
                            Description = entry.Description,
                            Order = entry.Order,
                            IsIndex = true,
                            Children = BuildChildrenRecursive(childSlug, currentSlug).ToList()
                        };

                        yield return node;
                        continue;
                    }
                }

                var dirRelSlug = _mapper.NormalizeSlug(
                    Path.GetRelativePath(_mapper.ContentRootPath, dir).Replace('\\', '/')
                );

                foreach (var bubbled in BuildChildrenRecursive(dirRelSlug, currentSlug))
                    yield return bubbled;
            }


            foreach (var file in Directory.EnumerateFiles(baseFolder, "*.md"))
            {
                if (Path.GetFileName(file).Equals(_opt.DefaultDocument, StringComparison.OrdinalIgnoreCase))
                    continue;
                var fileName = Path.GetFileName(file);

                if (fileName.Equals("readme.md", StringComparison.OrdinalIgnoreCase))
                    continue;

                var childSlug = _mapper.SlugFromFilePath(file);
                var entry = LoadEntry(childSlug, file);
                if (entry.NavHide) continue;

                yield return new NavNode
                {
                    Slug = entry.Slug,
                    Title = entry.Title,
                    Description = entry.Description,
                    Order = entry.Order,
                    IsIndex = false
                };
            }
        }

        private string? ResolveFolderPath(string slug)
        {
            var root = _mapper.ContentRootPath;

            if (string.IsNullOrWhiteSpace(slug))
                return root;

            return Path.Combine(root, slug.Replace('/', Path.DirectorySeparatorChar));
        }

        private string? ResolveIndexPath(string slug)
        {
            var folder = ResolveFolderPath(slug);
            if (folder is null) return null;
            return Path.Combine(folder, _opt.DefaultDocument);
        }

        private static void MarkCurrent(NavNode node, string currentSlug)
        {
            node.IsCurrent = string.Equals(node.Slug.Trim('/'), currentSlug.Trim('/'), StringComparison.OrdinalIgnoreCase);

            foreach (var child in node.Children)
            {
                MarkCurrent(child, currentSlug);
                if (child.IsCurrent || child.IsAncestorOfCurrent)
                    node.IsAncestorOfCurrent = true;
            }

            // current node is also an ancestor path “marker” for UI expansion
            if (node.IsCurrent)
                node.IsAncestorOfCurrent = true;

            // sort children
            node.Children = node.Children
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        public Task<List<DocEntry>> GetGuideIndexAsync(string host, CancellationToken ct = default)
        {
            var items = GetChildren("")
                .Where(x => !x.NavHide)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Task.FromResult(items);
        }
        private static string? ResolveAssetPath(string? raw, string slug)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            raw = raw.Trim();

            // Already absolute URL
            if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return raw;

            // Already absolute path (site-root)
            if (raw.StartsWith("/"))
                return raw;

            // Relative to the doc folder
            // slug is like "guide/template-game" or "guide/template-game/index"
            var cleanSlug = (slug ?? "").Trim('/');

            if (cleanSlug.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
                cleanSlug = cleanSlug[..^"/index".Length].TrimEnd('/');

            // folder base under content
            // -> /content/guide/template-game/ + raw
            return $"/content/{cleanSlug}/{raw}".Replace("\\", "/");
        }


        private sealed record CacheEntry(DateTime LastWriteUtc, MarkdownDoc Doc);
    }

}
