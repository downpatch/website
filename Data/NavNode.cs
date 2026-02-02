namespace downpatch.Data
{
    public sealed class NavNode
    {
        public required string Slug { get; init; }          // e.g. "guide/halo-mcc"
        public required string Title { get; init; }         // nav_title/title
        public string? Description { get; init; }
        public int Order { get; init; }
        public bool IsIndex { get; init; }                  // true for folder index.md
        public bool IsCurrent { get; set; }                 // set at render time
        public bool IsAncestorOfCurrent { get; set; }       // set at render time
        public List<NavNode> Children { get; set; } = new();
    }
}
