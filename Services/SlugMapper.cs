using downpatch.Data;
using Microsoft.Extensions.Options;

namespace downpatch.Services
{
    public sealed class SlugMapper
    {
        private readonly IWebHostEnvironment _env;
        private readonly MarkdownOptions _opt;

        public SlugMapper(IWebHostEnvironment env, IOptions<MarkdownOptions> opt)
        {
            _env = env;
            _opt = opt.Value;
        }

        public string ContentRootPath => Path.Combine(_env.ContentRootPath, _opt.ContentRoot);

        public string NormalizeSlug(string? slug)
        {
            slug ??= "";
            slug = slug.Trim();
            slug = slug.Trim('/');
            slug = slug.Replace('\\', '/');
            return slug;
        }

        public string ApplySubdomainPrefix(string slug, string host)
        {
            if (!_opt.EnableSubdomainFolders) return slug;

            var sub = GetSubdomain(host, _opt.PrimaryDomain);
            if (string.IsNullOrWhiteSpace(sub)) return slug;

            if (_opt.IgnoredSubdomains.Any(x => string.Equals(x, sub, StringComparison.OrdinalIgnoreCase)))
                return slug;

            // If slug is empty, map to sub/index.md
            return string.IsNullOrWhiteSpace(slug) ? sub : $"{sub}/{slug}";
        }

        public string MapSlugToPath(string slug)
        {
            var root = ContentRootPath;

            if (string.IsNullOrWhiteSpace(slug))
                return Path.Combine(root, _opt.DefaultDocument);

            var slugPath = slug.Replace('/', Path.DirectorySeparatorChar);

            var direct = Path.Combine(root, slugPath + ".md");
            var index = Path.Combine(root, slugPath, _opt.DefaultDocument);

            if (_opt.PreferIndexFiles)
            {
                if (File.Exists(index)) return index;
                if (File.Exists(direct)) return direct;
            }
            else
            {
                if (File.Exists(direct)) return direct;
                if (File.Exists(index)) return index;
            }

            // return something deterministic (store will File.Exists check anyway)
            return _opt.PreferIndexFiles ? index : direct;
        }

        public string SlugFromFilePath(string filePath)
        {
            var root = ContentRootPath;
            var rel = Path.GetRelativePath(root, filePath).Replace('\\', '/');

            if (rel.EndsWith("/" + _opt.DefaultDocument, StringComparison.OrdinalIgnoreCase))
            {
                rel = rel[..^("/" + _opt.DefaultDocument).Length];
            }
            else if (rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                rel = rel[..^3];
            }

            return rel.Trim('/');
        }

        private static string GetSubdomain(string host, string? primaryDomain)
        {
            host = host.Trim().TrimEnd('.');

            if (!string.IsNullOrWhiteSpace(primaryDomain))
            {
                primaryDomain = primaryDomain.Trim().Trim('.');
                if (host.EndsWith(primaryDomain, StringComparison.OrdinalIgnoreCase))
                {
                    var left = host[..^primaryDomain.Length].TrimEnd('.');
                    if (left.Length == 0) return "";
                    // could be "a.b" but we only take first label
                    return left.Split('.', StringSplitOptions.RemoveEmptyEntries)[^1];
                }
            }

            // Generic fallback: if 3+ labels, take first label
            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 3 ? parts[0] : "";
        }
    }
}
