namespace downpatch.Data
{
    public sealed class SiteBranding
    {
        public string SiteName { get; init; } = "downpatch.com";
        public string Tagline { get; init; } = "Speedrun getting started documentation";
        public string DefaultOgImage { get; init; } = "/assets/og/default.png";

        public string? TwitterHandle { get; init; } = null;     // "@downpatch"
        public string? GitHubUrl { get; init; } = null;         // "https://github.com/..."
        public string? ThemeColor { get; init; } = "#0b0f14";
    }
}
