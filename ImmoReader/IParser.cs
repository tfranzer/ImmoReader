namespace ImmoReader
{
    using AngleSharp;
    using AngleSharp.Dom;
    using System.Collections.Generic;

    internal interface IParser
    {
        string Type { get; }

        Url Parse(IDocument document, out IList<ImmoData> readData);

        int GetCount(IDocument document);
    }
}