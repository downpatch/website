using downpatch.Data;
using downpatch.Services;
using Microsoft.Extensions.Options;
using System.Text;
using System.Xml.Linq;

namespace downpatch.Endpoints
{
    public static class SitemapEndpoints
    {
        public static IEndpointRouteBuilder MapSitemap(this IEndpointRouteBuilder app)
        {
            app.MapGet("/sitemap.xml", (
                HttpContext ctx,
                MarkdownStore store,
                IOptions<MarkdownOptions> opt) =>
            {
                var max = Math.Clamp(opt.Value.MaxSitemapUrls, 1, 50_000);

                var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}".TrimEnd('/');

                XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
                var urlset = new XElement(ns + "urlset");

                int count = 0;
                foreach (var (slug, last) in store.EnumerateAll().OrderBy(x => x.slug))
                {
                    if (count++ >= max) break;

                    var loc = string.IsNullOrWhiteSpace(slug) ? $"{baseUrl}/" : $"{baseUrl}/guide/{slug}";
                    //trim "index" from the end of URLs
                    if (loc.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
                    {
                        loc = loc.Substring(0, loc.Length - 6);
                    }

                    urlset.Add(new XElement(ns + "url",
                        new XElement(ns + "loc", loc),
                        new XElement(ns + "lastmod", last.ToString("yyyy-MM-dd"))
                    ));
                }

                //Double check no duplicates (shouldn't be any, but just in case)
                var distinctUrls = urlset.Elements(ns + "url")
                    .GroupBy(x => x.Element(ns + "loc")?.Value)
                    .Select(g => g.First())
                    .ToList();

                urlset.RemoveAll();
                urlset.Add(distinctUrls);



                var doc = new XDocument(urlset);
                return Results.Text(doc.ToString(SaveOptions.DisableFormatting), "application/xml", Encoding.UTF8);
            });

            return app;
        }
    }

}
