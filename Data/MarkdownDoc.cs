namespace downpatch.Data
{
    public sealed class MarkdownDoc
    {
        public required string Slug { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }

        public string? GameName { get; init; }
        public string? LeaderboardUrl { get; init; }
        public string? TimingMethod { get; init; }
        public string? DownpatchRequired { get; init; } 
        public string? AllowedVersions { get; init; }
        public string? WhereToBuy { get; init; }
        public IReadOnlyList<string> Platforms { get; init; } = Array.Empty<string>();

        public string? Canonical { get; init; }
        public bool NoIndex { get; init; }
        public string? OgImage { get; init; }
        public string? SquareImage { get; init; }

        public required string Html { get; init; }
        public required DateTime LastModifiedUtc { get; init; }
        public required string ETag { get; init; }
    }

}
