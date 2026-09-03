// Renders guide content with accessible headings, media, tables, and custom strategy blocks.
using Markdig;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Downpatch.Web.Services;

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

        var cacheKey = $"page::accessible-v5::{entry.Slug}";

        if (_cache.TryGetValue(cacheKey, out RenderedPage cached) &&
            cached.LastModifiedUtc == entry.LastModifiedUtc)
        {
            page = cached;
            return true;
        }

        var markdown = File.ReadAllText(entry.FilePath, Encoding.UTF8);
        var (frontMatter, body) = ParseFrontMatter(markdown);
        var title = frontMatter.TryGetValue("title", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : entry.Slug;

        var placeholders = new Dictionary<string, string>();
        body = RewriteStrategyBlocks(body, placeholders);

        var htmlBody = Markdown.ToHtml(body, _pipeline);

        foreach (var pair in placeholders)
            htmlBody = htmlBody.Replace(pair.Key, pair.Value, StringComparison.Ordinal);

        htmlBody = RewriteYoutubeEmbeds(htmlBody);
        htmlBody = NormalizeHeadingLevels(htmlBody);
        htmlBody = NormalizeHeadingSequence(htmlBody);
        htmlBody = EnsureImageAlternatives(htmlBody);
        htmlBody = WrapStandaloneAnimatedImages(htmlBody);
        htmlBody = WrapTables(htmlBody);
        htmlBody = RewriteRelativeLinks(htmlBody, entry.Slug);

        page = new RenderedPage(
            Slug: entry.Slug,
            Title: title,
            HtmlBody: htmlBody,
            FrontMatter: frontMatter,
            LastModifiedUtc: entry.LastModifiedUtc);

        _cache.Set(cacheKey, page, new MemoryCacheEntryOptions()
            .SetSize(page.HtmlBody.Length * 2)
            .SetPriority(CacheItemPriority.Normal));

        return true;
    }

    private static (Dictionary<string, string> frontMatter, string body) ParseFrontMatter(string text)
    {
        var frontMatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!(text.StartsWith("---\n") || text.StartsWith("---\r\n")))
            return (frontMatter, text);

        var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0)
            return (frontMatter, text);

        var closingLineEnd = text.IndexOf('\n', end + 4);
        if (closingLineEnd < 0)
            closingLineEnd = end + 4;

        var header = text.Substring(4, end - 4);
        var body = text[(closingLineEnd + 1)..];

        foreach (var raw in header.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line[..colon].Trim();
            var val = line[(colon + 1)..].Trim();

            if (val.Length >= 2 &&
                ((val[0] == '"' && val[^1] == '"') || (val[0] == '\'' && val[^1] == '\'')))
            {
                val = val[1..^1];
            }

            frontMatter[key] = val;
        }

        return (frontMatter, body);
    }

    private string RewriteStrategyBlocks(string markdown, Dictionary<string, string> placeholders)
    {
        return Regex.Replace(
            markdown,
            @"^[ \t]*:::strategy[ \t]*\r?\n(?<content>.*?)^[ \t]*:::[ \t]*\r?$",
            match =>
            {
                var content = match.Groups["content"].Value;
                var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
                var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var bodyStart = lines.Length;

                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.Length == 0)
                        continue;

                    var colon = trimmed.IndexOf(':');

                    if (colon > 0 && IsStrategyMetadataKey(trimmed[..colon]))
                    {
                        data[trimmed[..colon].Trim()] = trimmed[(colon + 1)..].Trim();
                        continue;
                    }

                    bodyStart = i;
                    break;
                }

                var strategyBody = bodyStart < lines.Length
                    ? string.Join('\n', lines[bodyStart..]).Trim()
                    : "";

                string Raw(string key) => data.TryGetValue(key, out var item) ? item : "";
                string Encoded(string key) => WebUtility.HtmlEncode(Raw(key));

                var title = string.IsNullOrWhiteSpace(Raw("title")) ? "Strategy details" : Encoded("title");
                var renderedBody = string.IsNullOrWhiteSpace(strategyBody)
                    ? ""
                    : Markdown.ToHtml(strategyBody, _pipeline);
                var metadata = new StringBuilder();

                AppendMetadata(metadata, "Difficulty", Encoded("difficulty"),
                    $"strategy-card-difficulty {DifficultyClass(Raw("difficulty"))}");
                AppendMetadata(metadata, "Time save", Encoded("time-save"),
                    $"strategy-time-value {TimeSaveClass(Raw("time-save"))}",
                    string.IsNullOrWhiteSpace(Raw("compared-to"))
                        ? null
                        : $"over {Encoded("compared-to")}");
                AppendMetadata(metadata, "Platform", Encoded("platform"));
                AppendMetadata(metadata, "Input", Encoded("input"));
                AppendMetadata(metadata, "Recommended", Encoded("recommended"));
                AppendMetadata(metadata, "Consistency", Encoded("consistency"),
                    $"strategy-consistency {ConsistencyClass(Raw("consistency"))}");

                var placeholder = $"<!--DOWNPATCH_STRATEGY_{placeholders.Count}-->";
                placeholders[placeholder] = $"""
                    <div class="strategy-card">
                        <p class="strategy-card-title"><strong>{title}</strong></p>
                        <dl class="strategy-card-meta">
                            {metadata}
                        </dl>
                        <div class="strategy-card-content">
                            {renderedBody}
                        </div>
                    </div>
                    """;

                return placeholder;
            },
            RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    private static bool IsStrategyMetadataKey(string key) => key.Trim().ToLowerInvariant() is
        "title" or
        "difficulty" or
        "time-save" or
        "compared-to" or
        "platform" or
        "input" or
        "recommended" or
        "consistency";

    private static void AppendMetadata(
        StringBuilder html,
        string label,
        string value,
        string? cssClass = null,
        string? subtext = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var classAttribute = string.IsNullOrWhiteSpace(cssClass)
            ? ""
            : $" class=\"{cssClass.Trim()}\"";

        html.Append("<dt>").Append(label).Append("</dt><dd>")
            .Append("<span").Append(classAttribute).Append('>')
            .Append(value).Append("</span>");

        if (!string.IsNullOrWhiteSpace(subtext))
            html.Append("<span class=\"strategy-time-subtext\">").Append(subtext).Append("</span>");

        html.Append("</dd>");
    }

    private static string RewriteYoutubeEmbeds(string html)
    {
        return Regex.Replace(
            html,
            @"<youtube\b(?<attrs>[^>]*)>(?<body>.*?)</youtube>",
            match =>
            {
                var attrs = match.Groups["attrs"].Value;
                var body = WebUtility.HtmlDecode(match.Groups["body"].Value).Trim();
                var title = GetAttribute(attrs, "title");
                var id = GetAttribute(attrs, "id");
                var url = GetAttribute(attrs, "url");

                // Markdig expands <https://...> autolinks before this pass, so
                // accept either plain text/IDs or the generated anchor element.
                var linkedUrl = GetAttribute(body, "href");
                if (!string.IsNullOrWhiteSpace(linkedUrl))
                    body = linkedUrl;
                else
                    body = Regex.Replace(body, "<[^>]+>", "").Trim();

                body = body.Replace("\\", "", StringComparison.Ordinal).Trim();
                if (body.StartsWith('<') && body.EndsWith('>'))
                    body = body[1..^1].Trim();

                string? embedUrl = null;

                if (TryNormalizeYoutubeId(id, out var normalizedId))
                    embedUrl = BuildYoutubeEmbedUrl(normalizedId, null);
                else if (!string.IsNullOrWhiteSpace(url))
                    embedUrl = ExtractYoutubeEmbedUrl(url);
                else if (Uri.TryCreate(body, UriKind.Absolute, out _))
                    embedUrl = ExtractYoutubeEmbedUrl(body);
                else if (TryNormalizeYoutubeId(body, out normalizedId))
                    embedUrl = BuildYoutubeEmbedUrl(normalizedId, null);

                if (string.IsNullOrWhiteSpace(embedUrl))
                    return match.Value;

                var accessibleTitle = WebUtility.HtmlEncode(
                    string.IsNullOrWhiteSpace(title) ? "YouTube tutorial video" : title);

                return $"""
                    <iframe class="video-embed"
                            src="{embedUrl}"
                            title="{accessibleTitle}"
                            loading="lazy"
                            allow="accelerometer; encrypted-media; picture-in-picture"
                            allowfullscreen></iframe>
                    """;
            },
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    private static string NormalizeHeadingLevels(string html)
    {
        if (!Regex.IsMatch(html, @"<h1\b", RegexOptions.IgnoreCase))
            return html;

        return Regex.Replace(
            html,
            @"</?h(?<level>[1-6])(?<rest>\b[^>]*)>",
            match =>
            {
                var level = Math.Min(6, int.Parse(match.Groups["level"].Value) + 1);
                var slash = match.Value.StartsWith("</", StringComparison.Ordinal) ? "/" : "";
                return $"<{slash}h{level}{match.Groups["rest"].Value}>";
            },
            RegexOptions.IgnoreCase);
    }

    private static string NormalizeHeadingSequence(string html)
    {
        var previousLevel = 1;

        return Regex.Replace(
            html,
            @"<h(?<level>[1-6])(?<attrs>\b[^>]*)>(?<content>.*?)</h\k<level>>",
            match =>
            {
                var originalLevel = int.Parse(match.Groups["level"].Value);
                var level = Math.Clamp(originalLevel, 2, previousLevel + 1);
                previousLevel = level;

                return $"<h{level}{match.Groups["attrs"].Value}>{match.Groups["content"].Value}</h{level}>";
            },
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    private static string EnsureImageAlternatives(string html)
    {
        return Regex.Replace(
            html,
            @"<img\b[^>]*>",
            match =>
            {
                var tag = match.Value;
                if (Regex.IsMatch(tag, @"\salt\s*=", RegexOptions.IgnoreCase))
                    return tag;

                var title = GetAttribute(tag, "title") ?? "";
                var alt = WebUtility.HtmlEncode(WebUtility.HtmlDecode(title));
                var insertAt = tag.EndsWith("/>", StringComparison.Ordinal) ? tag.Length - 2 : tag.Length - 1;
                return tag.Insert(insertAt, $" alt=\"{alt}\"");
            },
            RegexOptions.IgnoreCase);
    }

    private static string WrapStandaloneAnimatedImages(string html)
    {
        var replacements = new List<string>();

        string Store(Match match)
        {
            var image = match.Groups["image"].Value;
            var alt = WebUtility.HtmlDecode(GetAttribute(image, "alt") ?? "").Trim();
            var summary = string.IsNullOrWhiteSpace(alt)
                ? "Show animated demonstration"
                : $"Show animated demonstration: {alt}";

            var replacement = $"""
                <details class="animated-media">
                    <summary>{WebUtility.HtmlEncode(summary)}</summary>
                    {image}
                </details>
                """;

            var token = $"<!--DOWNPATCH_ANIMATION_{replacements.Count}-->";
            replacements.Add(replacement);
            return token;
        }

        const string imagePattern = "(?<image><img\\b(?=[^>]*\\bsrc\\s*=\\s*\"[^\"]+\\.gif(?:\\?[^\"]*)?\")[^>]*>)";

        html = Regex.Replace(
            html,
            @"<p>(?<content>.*?)</p>",
            paragraph =>
            {
                var content = paragraph.Groups["content"].Value;
                var images = Regex.Matches(content, imagePattern, RegexOptions.IgnoreCase);
                if (images.Count == 0)
                    return paragraph.Value;

                var output = new StringBuilder();
                var position = 0;

                foreach (Match image in images)
                {
                    AppendParagraph(output, content[position..image.Index]);
                    output.Append(Store(image));
                    position = image.Index + image.Length;
                }

                AppendParagraph(output, content[position..]);
                return output.ToString();
            },
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        html = Regex.Replace(
            html,
            $@"(?m)^\s*{imagePattern}\s*$",
            Store,
            RegexOptions.IgnoreCase);

        for (var i = 0; i < replacements.Count; i++)
            html = html.Replace($"<!--DOWNPATCH_ANIMATION_{i}-->", replacements[i], StringComparison.Ordinal);

        return html;

        static void AppendParagraph(StringBuilder output, string content)
        {
            if (!string.IsNullOrWhiteSpace(content))
                output.Append("<p>").Append(content.Trim()).Append("</p>");
        }
    }

    private static string WrapTables(string html)
    {
        return Regex.Replace(
            html,
            @"(?<table><table\b.*?</table>)",
            match => $"<div class=\"table-scroll\" role=\"region\" aria-label=\"Scrollable data table\" tabindex=\"0\">{match.Groups["table"].Value}</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    private static string RewriteRelativeLinks(string html, string slug)
    {
        var lastSlash = slug.LastIndexOf('/');
        if (lastSlash < 0)
            return html;

        var basePath = "/" + slug[..lastSlash] + "/";
        var baseUri = new Uri("https://downpatch.local" + basePath);

        return Regex.Replace(
            html,
            "<a\\s+([^>]*?)href=\"(.*?)\"",
            match =>
            {
                var before = match.Groups[1].Value;
                var href = WebUtility.HtmlDecode(match.Groups[2].Value);

                if (href.StartsWith('/') || href.StartsWith('#') || href.StartsWith("//") ||
                    Uri.TryCreate(href, UriKind.Absolute, out _))
                {
                    return match.Value;
                }

                var suffixAt = href.IndexOfAny(['#', '?']);
                var path = suffixAt >= 0 ? href[..suffixAt] : href;
                var suffix = suffixAt >= 0 ? href[suffixAt..] : "";

                if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    path = path[..^3];

                if (!Uri.TryCreate(baseUri, path + suffix, out var resolved))
                    return match.Value;

                var newHref = resolved.PathAndQuery + resolved.Fragment;
                newHref = Regex.Replace(newHref, @"/index(?=[?#]|$)", "", RegexOptions.IgnoreCase);

                return $"<a {before}href=\"{WebUtility.HtmlEncode(newHref)}\"";
            },
            RegexOptions.IgnoreCase);
    }

    private static string? GetAttribute(string input, string name)
    {
        var match = Regex.Match(
            input,
            $@"\b{Regex.Escape(name)}\s*=\s*[""'](?<value>.*?)[""']",
            RegexOptions.IgnoreCase);

        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value) : null;
    }

    private static string DifficultyClass(string difficulty) => difficulty.Trim().ToLowerInvariant() switch
    {
        "beginner" => "beginner",
        "intermediate" => "intermediate",
        "advanced" => "advanced",
        "il only" => "il-only",
        "experimental" => "experimental",
        _ => ""
    };

    private static string TimeSaveClass(string value)
    {
        var match = Regex.Match(value, @"\d+");
        if (!match.Success)
            return "";

        var seconds = int.Parse(match.Value);
        if (seconds >= 45) return "legendary";
        if (seconds >= 30) return "major";
        if (seconds >= 15) return "great";
        if (seconds >= 5) return "good";
        return "minor";
    }

    private static string ConsistencyClass(string consistency)
    {
        var digits = new string(consistency.Where(char.IsDigit).ToArray());
        if (!int.TryParse(digits, out var value))
            return "";

        if (value >= 80) return "high";
        if (value >= 50) return "medium";
        return "low";
    }

    private static string? ExtractYoutubeEmbedUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var host = uri.Host.ToLowerInvariant();
        string? videoId;

        if (host is "youtu.be" or "www.youtu.be")
        {
            videoId = uri.AbsolutePath.Trim('/').Split('/')[0];
        }
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtube-nocookie.com" or "www.youtube-nocookie.com")
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            videoId = segments.Length >= 2 && segments[0] is "embed" or "shorts"
                ? segments[1]
                : System.Web.HttpUtility.ParseQueryString(uri.Query)["v"];
        }
        else
        {
            return null;
        }

        if (!TryNormalizeYoutubeId(videoId, out var normalizedId))
            return null;

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var start = ParseStartTime(query["t"] ?? query["start"]);
        return BuildYoutubeEmbedUrl(normalizedId, start);
    }

    private static bool TryNormalizeYoutubeId(string? value, out string normalized)
    {
        normalized = (value ?? "").Replace("\\", "", StringComparison.Ordinal).Trim();
        return Regex.IsMatch(normalized, @"^[A-Za-z0-9_-]{6,20}$");
    }

    private static string BuildYoutubeEmbedUrl(string videoId, int? start)
    {
        var url = $"https://www.youtube-nocookie.com/embed/{videoId}";
        return start is > 0 ? $"{url}?start={start}" : url;
    }

    private static int? ParseStartTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim().ToLowerInvariant();
        if (int.TryParse(value.TrimEnd('s'), out var seconds))
            return seconds;

        var match = Regex.Match(value, @"^(?:(?<h>\d+)h)?(?:(?<m>\d+)m)?(?:(?<s>\d+)s)?$");
        if (!match.Success)
            return null;

        var hours = match.Groups["h"].Success ? int.Parse(match.Groups["h"].Value) : 0;
        var minutes = match.Groups["m"].Success ? int.Parse(match.Groups["m"].Value) : 0;
        var remainingSeconds = match.Groups["s"].Success ? int.Parse(match.Groups["s"].Value) : 0;
        return (hours * 3600) + (minutes * 60) + remainingSeconds;
    }

    public readonly record struct RenderedPage(
        string Slug,
        string Title,
        string HtmlBody,
        IReadOnlyDictionary<string, string> FrontMatter,
        DateTime LastModifiedUtc);
}
