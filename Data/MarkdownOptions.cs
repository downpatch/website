namespace downpatch.Data
{
    public sealed class MarkdownOptions
    {
        public const string SectionName = "Markdown";

        public string ContentRoot { get; set; } = "Content";
        public string DefaultDocument { get; set; } = "index.md";
        public bool PreferIndexFiles { get; set; } = true;

        public bool EnableSubdomainFolders { get; set; } = true;
        public string[] IgnoredSubdomains { get; set; } = ["www"];
        public string? PrimaryDomain { get; set; } = null;

        public int CacheSeconds { get; set; } = 300;
        public int MaxSitemapUrls { get; set; } = 50_000;
    }

}
