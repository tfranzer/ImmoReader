using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmoReader
{
    class Immoscout24Parser : IParser
    {
        public string Parse(IDocument document)
        {

            // 
            return FindNextPage(document);
        }

        private string FindNextPage(IDocument document)
        {
            var result = document.QuerySelectorAll<IHtmlAnchorElement>("a").Where(anchor => anchor.Dataset["nav-next-page"] == "true").ToList();

            
            if (result.Count == 0) { return null; }
            if (result.Count == 1)
            {
                return result[0].Href;
            }

            throw new ArgumentException("More than one Next page entries found");
        }
    }
}
