namespace ImmoReader
{
    using AngleSharp.Dom;

    internal interface IParser
    {
        string Type { get; }

        string Parse(IDocument document, out int count);

        int GetCount(IDocument document);
    }
}