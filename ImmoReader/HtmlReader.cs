namespace ImmoReader
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

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

        internal IList<ImmoData> Read(Url url)
        {
            var document = url.Open();
            var totalCount = this.parser.GetCount(document);
            var readCount = 0;

            var allReadData = new List<ImmoData>();
            Trace.WriteLine($"Reading ~{totalCount} objects for {this.parser.Type}");
            while (readCount < totalCount)
            {
                url = this.parser.Parse(document, out var readData);
                readCount += readData.Count;
                allReadData.AddRange(readData);

                if (url == null)
                {
                    return allReadData;
                }

                document = url.Open();
            }

            return allReadData;
        }
    }
}