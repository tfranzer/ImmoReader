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
        private string dataPath;
        public string Type => ImmoPageType.Immoscout24.ToString();
        internal Immoscout24Parser(string dataPath)
        {
            this.dataPath = dataPath;
        }

        public int GetCount(IDocument document)
        {
            return 0;
            //return int.Parse(Regex.Match(document.QuerySelectorAll<IHtmlSpanElement>("span").Where(span => span.Id == "totalCount").First().Text().Trim(), @"\d+").Value);
        }

        public string Parse(IDocument document, out int count)
        {
            count = 0;
            return null;
            //return FindNextPage(document);
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

        private void ParseObject(IHtmlDivElement divElement)
        {

        }
    }
}
