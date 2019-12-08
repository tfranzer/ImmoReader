using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImmoReader
{
    interface IParser
    {
        string Parse(IDocument document, out int count);

        int GetCount(IDocument document);

        string Type { get; }
    }
}
