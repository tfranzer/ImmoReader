namespace ImmoReader
{
    using System;
    
    using AngleSharp;

    internal class HtmlReader
    {
        private readonly IParser parser;

        internal HtmlReader(string dataPath, ImmoPageType immoPageType)
        {
            switch (immoPageType)
            {
                case ImmoPageType.Immonet:
                    this.parser = new ImmonetParser(dataPath);
                    break;
                case ImmoPageType.Immoscout24:
                    this.parser = new Immoscout24Parser(dataPath);
                    break;
                default:
                    throw new ArgumentException($"Unknown type {immoPageType}");
            }
        }

        internal void Read(Url url)
        {
            var document = url.Open();
            var totalCount = this.parser.GetCount(document);
            var readCount = 0;

            Console.WriteLine($"Reading ~{totalCount} objects for {this.parser.Type}");
            while (readCount < totalCount)
            {
                url = this.parser.Parse(document, out var count);
                readCount += count;

                if (url == null)
                {
                    return;
                }

                document = url.Open();
            }
        }
    }
}