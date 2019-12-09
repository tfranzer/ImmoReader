namespace ImmoReader
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Html.Parser;

    internal class HtmlReader
    {
        private readonly string dataPath;

        private readonly IParser parser;

        private HtmlParser htmlParser = new HtmlParser();

        internal HtmlReader(string dataPath, ImmoPageType immoPageType)
        {
            this.dataPath = Path.Combine(dataPath, immoPageType.ToString());
            Directory.CreateDirectory(this.dataPath);

            switch (immoPageType)
            {
                case ImmoPageType.Immonet:
                    this.parser = new ImmonetParser(this.dataPath);
                    break;
                case ImmoPageType.Immoscout24:
                    this.parser = new Immoscout24Parser(this.dataPath);
                    break;
                default:
                    throw new ArgumentException($"Unknown type {immoPageType}");
            }
        }

        internal async Task Read(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            var document = await BrowsingContext.New(AngleSharp.Configuration.Default.WithDefaultLoader()).OpenAsync(url);
            var totalCount = this.parser.GetCount(document);
            var readCount = 0;

            Console.WriteLine($"Reading ~{totalCount} objects for {this.parser.Type}");
            while (readCount < totalCount)
            {
                url = this.parser.Parse(document, out var count);
                readCount += count;

                if (string.IsNullOrEmpty(url))
                {
                    return;
                }

                document = await BrowsingContext.New(AngleSharp.Configuration.Default.WithDefaultLoader()).OpenAsync(url);
            }
        }
    }
}