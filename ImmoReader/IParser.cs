namespace ImmoReader
{
    using AngleSharp;
    using AngleSharp.Dom;

    internal interface IParser
    {
        string Type { get; }

        Url Parse(IDocument document, out int count);

        int GetCount(IDocument document);
    }
}