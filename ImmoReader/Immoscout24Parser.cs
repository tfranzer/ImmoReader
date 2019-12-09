namespace ImmoReader
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class Immoscout24Parser : IParser
    {
        private readonly string dataPath;

        internal Immoscout24Parser(string dataPath)
        {
            this.dataPath = dataPath;
        }

        public string Type => ImmoPageType.Immoscout24.ToString();

        public int GetCount(IDocument document)
        {
            return int.Parse(
                Regex.Match(
                    document.QuerySelectorAll<IHtmlSpanElement>("span").Where(span => span.Dataset["is24-qa"] == "resultlist-resultCount").First().Text()
                        .Trim(),
                    @"\d+").Value);
        }

        public string Parse(IDocument document, out int count)
        {
            // Parse all entries
            var elements = document.QuerySelectorAll<IHtmlListItemElement>("li").Where(div => div.ClassList.Contains("result-list__listing"));
            Parallel.ForEach(
                elements,
                element =>
                    {
                        try
                        {
                            this.ParseObject(element);
                        }
                        catch
                        {
                            Console.WriteLine($"Failed to parse {element.Dataset["id"]}");
                        }
                    });

            count = elements.Count();
            return this.FindNextPage(document);
        }

        private string FindNextPage(IDocument document)
        {
            var result = document.QuerySelectorAll<IHtmlAnchorElement>("a").Where(anchor => anchor.Dataset["nav-next-page"] == "true").ToList();

            if (result.Count == 0)
            {
                return null;
            }

            if (result.Count == 1)
            {
                return result[0].Href;
            }

            throw new ArgumentException("More than one Next page entries found");
        }

        private void ParseObject(IHtmlListItemElement element)
        {
            var id = element.Dataset["id"];
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
                data = new ImmoData { Id = id, InitialDate = DateTime.Today };
            }

            // get details url
            var detailsElement = element.QuerySelectorAll<IHtmlAnchorElement>("a").Where(anchor => anchor.Dataset["go-to-expose-id"] == id).First();

            Console.WriteLine($"Reading {detailsElement.Href}");

            // get image url
            try
            {
                data.ImageFileName = this.LoadImage(new Uri(detailsElement.QuerySelector<IHtmlImageElement>("img").Dataset["lazy-src"]), idPath);
            }
            catch (Exception)
            {
                Console.WriteLine($"Failed to load image for {id}");
            }

            // get details from details page
            this.ParseDetails(detailsElement.Href, data);

            // save data
            File.WriteAllText(idFilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private JObject ReadJson(string content, string token)
        {
            try
            {
                var idx = content.IndexOf(token);
                var idxStart = content.IndexOf('{', idx + token.Length);
                var idxEnd = content.IndexOf('}', idx);
                return (JObject)JsonConvert.DeserializeObject(content.Substring(idxStart, idxEnd - idxStart + 1));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ParseDetails(string detailsUrl, ImmoData data)
        {
            data.Url = detailsUrl;

            var detailsDocument = BrowsingContext.New(AngleSharp.Configuration.Default.WithDefaultLoader()).OpenAsync(detailsUrl).Result;

            // Date and price
            data.LastDate = DateTime.Today;

            var priceElement = detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div.ClassList.Contains("is24qa-kaufpreis"))
                .FirstOrDefault();
            if (priceElement != null)
            {
                data.LastPrice = int.Parse(Regex.Match(priceElement.Text().Replace(".", string.Empty), @"\d+").Value);

                if (data.InitialPrice == null)
                {
                    data.InitialPrice = data.LastPrice;
                }
            }

            // Title
            data.Title = detailsDocument.QuerySelectorAll("h1").Where(div => div.Id == "expose-title").FirstOrDefault()?.Text().Trim();

            var fullText = detailsDocument.All[0].Text();

            // Location Link
            var locationObject = this.ReadJson(fullText, "solarPotential:");
            if (locationObject != null)
            {
                var url = locationObject["linkUrl"].ToString();
                const string latToken = "lat=";
                const string lonToken = "lon=";
                var startIdx = url.IndexOf(latToken) + latToken.Length;
                var endIdx = url.IndexOf('&', startIdx);
                var lat = url.Substring(startIdx, endIdx - startIdx);
                startIdx = url.IndexOf(lonToken) + lonToken.Length;
                endIdx = url.IndexOf('&', startIdx);
                var lon = url.Substring(startIdx, endIdx - startIdx);
                data.LocationUrl = $"https://www.google.com/maps/search/{lat},{lon}";
            }

            // broker
            var realtorObject = this.ReadJson(fullText, "\"realtor\":");
            if (realtorObject != null)
            {
                data.Broker = $"{realtorObject["firstName"]} {realtorObject["lastName"]}";
            }

            //data.BrokerFirm = contactBox.QuerySelectorAll<IHtmlSpanElement>("span").Where(div => div.Dataset["qa"] == "company-name").FirstOrDefault()?.Text().Trim();

            // living size
            data.LivingSize = int.Parse(
                Regex.Match(
                    detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div.ClassList.Contains("is24qa-wohnflaeche")).First().Text()
                        .Replace(".", string.Empty).Trim(),
                    @"\d+").Value);

            // ground size
            data.GroundSize = int.Parse(
                Regex.Match(
                    detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div.ClassList.Contains("is24qa-grundstueck")).First().Text()
                        .Replace(".", string.Empty).Trim(),
                    @"\d+").Value);

            // room number
            var roomsElement = detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div.ClassList.Contains("is24qa-zi")).FirstOrDefault();
            if (roomsElement != null)
            {
                data.RoomCount = int.Parse(Regex.Match(roomsElement.Text().Trim(), @"\d+").Value);
            }

            // Title
            data.Title = detailsDocument.QuerySelectorAll<IHtmlElement>("h1").Where(div => div?.Id == "expose-title").FirstOrDefault()?.Text().Trim();

            // Year TODO!
            if (int.TryParse(
                Regex.Match(
                    detailsDocument.QuerySelectorAll("dd").Where(div => div.ClassList.Contains("is24qa-baujahr")).FirstOrDefault()?.Text().Trim()
                    ?? string.Empty,
                    @"\d+").Value,
                out var year))
            {
                data.Year = year;
            }

            // Tags
            var tagsElement = detailsDocument.QuerySelectorAll<IHtmlDivElement>("div")
                .Where(div => div.ClassList.Contains(new[] { "criteriagroup", "boolean-listing" })).FirstOrDefault();
            if (tagsElement != null)
            {
                data.Tags = tagsElement.Children.Select(child => child.Text()).ToHashSet();
            }

            // Location
            data.Location = detailsDocument.QuerySelectorAll<IHtmlSpanElement>("span").Where(div => div.ClassList.Contains("zip-region-and-country"))
                .FirstOrDefault()?.Text().Trim();

            // Type
            data.Type = detailsDocument.QuerySelectorAll("dd").Where(div => div.ClassList.Contains("is24qa-typ")).FirstOrDefault()?.Text().Trim();
        }

        private string LoadImage(Uri imageUrl, string dataPath)
        {
            var fileName = imageUrl.Segments[2].Replace("/", string.Empty);
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
    }
}