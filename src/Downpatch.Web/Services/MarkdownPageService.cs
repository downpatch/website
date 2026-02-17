using Downpatch.Web.Services;
using Markdig;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace Downpatch.Web.Services
{
    public sealed class MarkdownPageService
    {
        private readonly ContentIndex _index;
        private readonly IMemoryCache _cache;
        private readonly MarkdownPipeline _pipeline;

        public MarkdownPageService(ContentIndex index, IMemoryCache cache, MarkdownPipeline pipeline)
        {
            _index = index;
            _cache = cache;
            _pipeline = pipeline;
        }

        public void WarmAll()
        {
            foreach (var entry in _index.AllEntries)
            {
                _ = TryGetRendered(entry.Slug.StartsWith("guide/", StringComparison.OrdinalIgnoreCase)
                    ? entry.Slug["guide/".Length..]
                    : entry.Slug, out _);
            }
        }

        public bool TryGetRendered(string? slug, out RenderedPage page)
        {
            page = default;

            if (!_index.TryResolve(slug, out var entry))
                return false;

            var cacheKey = $"page::{entry.Slug}";

            if (_cache.TryGetValue(cacheKey, out RenderedPage cached) && cached.LastModifiedUtc == entry.LastModifiedUtc)
            {
                page = cached;
                return true;
            }

            var markdown = File.ReadAllText(entry.FilePath, Encoding.UTF8);
            var (fm, body) = ParseFrontMatter(markdown);

            var title = fm.TryGetValue("title", out var t) && !string.IsNullOrWhiteSpace(t) ? t : entry.Slug;
            var htmlBody = Markdown.ToHtml(body, _pipeline);
            htmlBody = RewriteRelativeLinks(htmlBody, entry.Slug);

            page = new RenderedPage(
                Slug: entry.Slug,
                Title: title,
                HtmlBody: htmlBody,
                FrontMatter: fm,
                LastModifiedUtc: entry.LastModifiedUtc
            );

            var approxBytes = page.HtmlBody.Length * 2;

            _cache.Set(cacheKey, page, new MemoryCacheEntryOptions()
                .SetSize(approxBytes)
                .SetPriority(CacheItemPriority.Normal));

            return true;
        }

        private static (Dictionary<string, string> frontMatter, string body) ParseFrontMatter(string text)
        {
            var fm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!(text.StartsWith("---\n") || text.StartsWith("---\r\n")))
                return (fm, text);

            var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
            if (end < 0)
                return (fm, text);

            var closingLineEnd = text.IndexOf('\n', end + 4);
            if (closingLineEnd < 0) closingLineEnd = end + 4;

            var header = text.Substring(4, end - 4);
            var body = text.Substring(closingLineEnd + 1);

            foreach (var raw in header.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                var colon = line.IndexOf(':');
                if (colon <= 0) continue;

                var key = line[..colon].Trim();
                var val = line[(colon + 1)..].Trim();

                if (val.Length >= 2 && ((val[0] == '"' && val[^1] == '"') || (val[0] == '\'' && val[^1] == '\'')))
                    val = val[1..^1];

                fm[key] = val;
            }

            return (fm, body);
        }

        private static string RewriteRelativeLinks(string html, string slug)
        {
            // slug example: guide/halo/index
            // base path should be /guide/halo/
            var lastSlash = slug.LastIndexOf('/');
            if (lastSlash < 0)
                return html;

            var basePath = "/" + slug[..lastSlash] + "/";

            return System.Text.RegularExpressions.Regex.Replace(
                html,
                "<a\\s+([^>]*?)href=\"(.*?)\"",
                match =>
                {
                    var before = match.Groups[1].Value;
                    var href = match.Groups[2].Value;

                    // skip absolute + root links
                    if (href.StartsWith("/") ||
                        href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                        href.StartsWith("#") ||
                        href.StartsWith("mailto:"))
                        return match.Value;

                    // remove .md if present
                    if (href.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                        href = href[..^3];

                    var newHref = basePath + href.TrimStart('/');

                    return $"<a {before}href=\"{newHref}\"";
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }


        public readonly record struct RenderedPage(
            string Slug,
            string Title,
            string HtmlBody,
            IReadOnlyDictionary<string, string> FrontMatter,
            DateTime LastModifiedUtc
        );
    }
}
