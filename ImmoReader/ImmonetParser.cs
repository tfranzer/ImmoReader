namespace ImmoReader
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;

    using Newtonsoft.Json;

    internal class ImmonetParser : IParser
    {
        private const string idPrefix = "selObject_";

        private readonly string dataPath;

        internal ImmonetParser(string dataPath)
        {
            this.dataPath = dataPath;
        }

        public string Type => ImmoPageType.Immonet.ToString();

        public int GetCount(IDocument document)
        {
            return 0;
            return int.Parse(
                Regex.Match(document.QuerySelectorAll<IHtmlSpanElement>("span").Where(span => span.Id == "totalCount").First().Text().Trim(), @"\d+").Value);
        }

        public Url Parse(IDocument document, out int count)
        {
            count = 0;
            return null;
            // Parse all entries
            var elements = document.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div.Id?.StartsWith(idPrefix) ?? false);
            Parallel.ForEach(
                elements,
                divElement =>
                    {
                        try
                        {
                            this.ParseObject(divElement);
                        }
                        catch
                        {
                            Console.WriteLine($"Failed to parse {divElement.Id.Remove(0, idPrefix.Length)}");
                        }
                    });

            count = elements.Count();
            return this.FindNextPage(document);
        }

        private Url FindNextPage(IDocument document)
        {
            var result = document.QuerySelectorAll<IHtmlAnchorElement>("a").Where(anchor => anchor.ClassList.Contains(new[] { "pull-right", "text-right" }))
                .ToList();

            if (result.Count == 0)
            {
                return null;
            }

            if (result.Count == 1)
            {
                return new Url(result[0].Href);
            }

            throw new ArgumentException("More than one Next page entries found");
        }

        private void ParseObject(IHtmlDivElement divElement)
        {
            var id = divElement.Id.Remove(0, idPrefix.Length);
            var idPath = Path.Combine(this.dataPath, id);
            Directory.CreateDirectory(idPath);

            var idFilePath = Path.Combine(idPath, $"{id}.json");

            ImmoData data = null;
            if (File.Exists(idFilePath))
            {
                data = JsonConvert.DeserializeObject<ImmoData>(File.ReadAllText(idFilePath));
            }
            else
            {
                data = new ImmoData { Id = id, FirstSeenDate = DateTime.Today };
            }

            // Title, Location and Type
            var descriptionParts = divElement.QuerySelectorAll<IHtmlSpanElement>("span").Where(span => span.ClassList.Contains("text-100")).First().Text()
                .Split("•");

            if (descriptionParts.Length == 3)
            {
                data.Location = descriptionParts[2].Trim().Replace("\n\t\t\n\t\t\n\t\t\t", " ");
                data.Type = descriptionParts[1].Trim();

                var distanceText = descriptionParts[0].Trim().Replace("km", string.Empty).Trim();
                data.Distance = decimal.Round(decimal.Parse(descriptionParts[0].Trim().Replace("km", string.Empty).Trim(), CultureInfo.InvariantCulture), 2);
            }

            // Tags
            var tagElements = divElement.QuerySelectorAll<IHtmlSpanElement>("span").Where(span => span.ClassList.Contains("tag-element-50"));
            data.Tags = tagElements.Select(span => span.Text()).ToHashSet();

            // get details url
            var results = divElement.QuerySelectorAll<IHtmlAnchorElement>("a").Where(anchor => anchor.Id?.StartsWith("lnkImgToDetails_") ?? false).ToList();

            if (results.Count == 0)
            {
                return;
            }

            if (results.Count == 1)
            {
                var detailsElement = results[0];
                Console.WriteLine($"Reading {detailsElement.Href}");

                // get image url
                try
                {
                    data.ImageFileName = this.LoadImage(new Uri(detailsElement.QuerySelector<IHtmlImageElement>("img").Dataset["original"]), idPath);
                }
                catch (Exception)
                {
                    Console.WriteLine($"Failed to load image for {id}");
                }

                // get details from details page
                this.ParseDetails(detailsElement.Href, data);
            }
            else
            {
                throw new ArgumentException("More than one link to details found");
            }

            // save data
            File.WriteAllText(idFilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private string LoadImage(Uri imageUrl, string dataPath)
        {
            var fileName = imageUrl.Segments.Last();
            var imagePath = Path.Combine(dataPath, fileName);

            if (!File.Exists(imagePath))
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(imageUrl);
                var httpWebReponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var stream = httpWebReponse.GetResponseStream())

                using (var fileStream = File.Create(imagePath))
                {
                    stream.CopyTo(fileStream);
                }
            }

            return fileName;
        }

        private void ParseDetails(string detailsUrl, ImmoData data)
        {
            data.Url = detailsUrl;

            var detailsDocument = BrowsingContext.New(AngleSharp.Configuration.Default.WithDefaultLoader()).OpenAsync(detailsUrl).Result;

            // Date and price
            data.LastSeenDate = DateTime.Today;

            var priceElement = detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div.Id == "priceid_1").FirstOrDefault();
            if (priceElement != null)
            {
                data.LastPrice = int.Parse(Regex.Match(priceElement.Text(), @"\d+").Value);

                if (data.InitialPrice == null)
                {
                    data.InitialPrice = data.LastPrice;
                }
            }

            // broker
            data.Realtor = detailsDocument.QuerySelectorAll<IHtmlParagraphElement>("p").Where(div => div?.Id == "bdlName").FirstOrDefault()?.Text().Trim();

            data.RealtorCompany = detailsDocument.QuerySelectorAll<IHtmlParagraphElement>("p").Where(div => div?.Id == "bdlFirmname").FirstOrDefault()?.Text()
                .Trim();

            // living size
            data.LivingArea = int.Parse(
                Regex.Match(detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div?.Id == "areaid_1").First().Text().Trim(), @"\d+").Value);

            // living size
            data.SiteArea = int.Parse(
                Regex.Match(detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div?.Id == "areaid_3").First().Text().Trim(), @"\d+").Value);

            // room number
            var roomsElement = detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div?.Id == "equipmentid_1").FirstOrDefault();
            if (roomsElement != null)
            {
                data.RoomCount = int.Parse(Regex.Match(roomsElement.Text().Trim(), @"\d+").Value);
            }

            // Title
            data.Title = detailsDocument.QuerySelectorAll<IHtmlElement>("h1").Where(div => div?.Id == "expose-headline").FirstOrDefault()?.Text().Trim();

            // Year
            if (int.TryParse(
                Regex.Match(
                    detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div?.Id == "yearbuild").FirstOrDefault()?.Text().Trim()
                    ?? string.Empty,
                    @"\d+").Value,
                out var year))
            {
                data.Year = year;
            }
        }
    }
}