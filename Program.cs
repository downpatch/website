using downpatch.Components;
using downpatch.Data;
using downpatch.Services;
using Markdig.Renderers;

namespace downpatch
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddRouting();
            builder.Services.Configure<MarkdownOptions>(
            builder.Configuration.GetSection(MarkdownOptions.SectionName));

            builder.Services.AddSingleton<MarkdownStore>();
            builder.Services.AddSingleton<MarkdownRenderer>();

            builder.Services.AddSingleton(new SiteBranding
            {
                SiteName = "downpatch.com",
                Tagline = "A source of truth for getting started with speedrunning.",
                DefaultOgImage = "/assets/og/default.png",
                TwitterHandle = null,
                GitHubUrl = "https://github.com/downpatch",
                ThemeColor = "#0b0f14"
            });
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

            app.UseAntiforgery();
            app.UseStaticFiles();

            app.MapStaticAssets();
            app.MapRazorComponents<App>();
            app.UseForwardedHeaders();
            app.Run();
        }
    }
}
