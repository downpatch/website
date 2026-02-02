using downpatch.Data;
using Markdig;
using Markdig.Renderers.Html;

namespace downpatch.Services
{
    public sealed class MarkdownRenderer
    {
        private readonly MarkdownPipeline _pipeline;

        public MarkdownRenderer()
        {
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseAutoIdentifiers()
                .Build();
        }
        public (string html, List<Heading>) ToHtmlWithToc(string markdown)
        {
            var doc = Markdig.Markdown.Parse(markdown, _pipeline);

            var headings = doc
                .OfType<Markdig.Syntax.HeadingBlock>()
                .Where(h => h.Level >= 2 && h.Level <= 3)
                .Select(h => new Heading
                {
                    Level = h.Level,
                    Id = h.GetAttributes().Id,
                    Text = h.Inline?.ToString() ?? ""
                })
                .ToList();

            var html = Markdig.Markdown.ToHtml(doc, _pipeline);
            return (html, headings);
        }

        public string ToHtml(string markdown)
            => Markdown.ToHtml(markdown, _pipeline);
    }

}
