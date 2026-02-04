namespace downpatch.Endpoints
{
    public static class RobotsEndpoints
    {
        public static IEndpointRouteBuilder MapRobots(this IEndpointRouteBuilder app)
        {
            app.MapGet("/robots.txt", (HttpContext ctx) =>
            {
                var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}".TrimEnd('/');
                var txt =
    $@"User-agent: *
Allow: /

Sitemap: {baseUrl}/sitemap.xml
";
                return Results.Text(txt, "text/plain");
            });

            return app;
        }
    }

}
