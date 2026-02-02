namespace downpatch.Data
{
    public sealed class DocEntry
    {
        public required string Slug { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }
        public int Order { get; init; }
        public bool NavHide { get; init; }
        public bool IsIndex { get; init; }
    }
}
