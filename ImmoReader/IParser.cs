using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImmoReader
{
    interface IParser
    {
        string Parse(IDocument document);
    }
}
