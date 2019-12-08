using AngleSharp;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ImmoReader
{
    class HtmlReader
    {
        private string dataPath;
        private IParser parser;
        
        HtmlParser htmlParser = new HtmlParser();
        internal HtmlReader(string dataPath, ImmoPageType immoPageType)
        {
            this.dataPath = Path.Combine(dataPath, immoPageType.ToString());
            Directory.CreateDirectory(this.dataPath);

            switch(immoPageType)
            {
                case ImmoPageType.Immonet:
                    this.parser = new ImmonetParser();
                    break;
                case ImmoPageType.Immoscout24:
                    this.parser = new Immoscout24Parser();
                    break;
                default:
                    throw new ArgumentException($"Unknown type {immoPageType}");
            }
        }

        internal async Task Read(string url)
        {
            if(string.IsNullOrEmpty(url))
            { return; }

            Console.WriteLine($"Reading {url}");

            var context = BrowsingContext.New(AngleSharp.Configuration.Default.WithDefaultLoader());
            var document = await context.OpenAsync(url);
            await Read(this.parser.Parse(document)).ConfigureAwait(false);
        }
    }
}
