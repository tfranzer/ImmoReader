namespace ImmoReader
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;

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
            return document.Get("span", span => span.Dataset["is24-qa"] == "resultlist-resultCount").First().Text().ParseToInt().GetValueOrDefault(0);
        }

        public Url Parse(IDocument document, out int count)
        {
            // Parse all entries
            var listingElements = document.Get("li", div => div.ClassList.Contains("result-list__listing"));
            Parallel.ForEach(
                listingElements,
                element =>
                    {
                        try
                        {
                            this.ParseObject(element);
                        }
                        catch
                        {
                            Trace.WriteLine($"Failed to parse {element.Dataset["id"]}");
                        }
                    });

            count = listingElements.Count();
            return FindNextPage(document);
        }

        private static Url FindNextPage(IDocument document)
        {
            var result = document.Get<IHtmlAnchorElement>("a", anchor => anchor.Dataset["nav-next-page"] == "true").ToList();

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

        private static void ParseDetails(Url url, ImmoData data)
        {
            var detailsDocument = url.Open();

            // Title
            data.Title = detailsDocument.Title;

            var fullText = detailsDocument.All[0].Text();

            // Broker
            var exposeObject = Helper.ReadJson(fullText, "IS24.expose =");
            if (exposeObject != null)
            {
                var person = exposeObject["contactData"]["contactPerson"];
                if (person != null)
                {
                    data.Realtor = $"{person["firstName"]} {person["lastName"]}";
                }

                data.RealtorCompany = exposeObject["contactData"]["realtorInformation"]["companyName"]?.ToString();
            }

            // Location
            var quickCheckObject = Helper.ReadJson(fullText, "IS24.expose.quickCheckConfig =");
            if (quickCheckObject != null)
            {
                var urlParts = quickCheckObject["quickCheckServiceUrl"].ToString().Split('/', '&', '?');

                var lat = urlParts[5];
                var lon = urlParts[7];
                data.LocationUrl = $"https://www.google.com/maps/search/{lat},{lon}";
            }

            var premiumStatsObject = Helper.ReadJson(fullText, "IS24.premiumStatsWidget =", endToken:"}");
            if (premiumStatsObject != null)
            {
                data.OnlineSince = DateTime.Parse(premiumStatsObject["exposeOnlineSince"].ToString());
            }

            // Living Area
            data.LivingArea = detailsDocument.Get("div", div => div.ClassList.Contains("is24qa-wohnflaeche")).First().Text().ParseToInt();

            // Site area
            data.SiteArea = detailsDocument.Get("div", div => div.ClassList.Contains("is24qa-grundstueck")).First().Text().ParseToInt();

            // room number
            var roomsElement = detailsDocument.Get("div", div => div.ClassList.Contains("is24qa-zi")).FirstOrDefault();
            if (roomsElement != null)
            {
                data.RoomCount = roomsElement.Text().ParseToInt();
            }

            // Year
            data.Year = detailsDocument.Get("dd", div => div.ClassList.Contains("is24qa-baujahr")).FirstOrDefault()?.Text().ParseToInt();

            // Tags
            var tagsElement = detailsDocument.Get("div", div => div.ClassList.Contains(new[] { "criteriagroup", "boolean-listing" })).FirstOrDefault();
            if (tagsElement != null)
            {
                data.Tags = tagsElement.Children.Select(child => child.Text()).ToHashSet();
            }

            // Location
            data.Location = detailsDocument.Get("span", div => div.ClassList.Contains("zip-region-and-country")).FirstOrDefault()?.Text().Trim();

            // Type
            data.Type = detailsDocument.Get("dd", div => div.ClassList.Contains("is24qa-typ")).FirstOrDefault()?.Text().Trim();
        }

        private static string LoadImage(Uri imageUrl, string dataPath, string id)
        {
            var fileName = $"{id}{new FileInfo(imageUrl.Segments[2].Replace("/", string.Empty)).Extension}";

            Helper.LoadImage(imageUrl, Path.Combine(dataPath, fileName));

            return fileName;
        }

        private void ParseObject(IHtmlElement element)
        {
            var objectId = element.Dataset["id"];
            var id = $"{this.Type}-{objectId}";
            
            var data = Helper.GetImmoData(this.dataPath, id);
            var needDetails = string.IsNullOrEmpty(data.Url);

            // Date and price
            data.LastSeenDate = DateTime.Today;

            var priceElement = element.Get("dl", div => div.ClassList.Contains("result-list-entry__primary-criterion")).FirstOrDefault()?.Children[0];
            if (priceElement != null)
            {
                var price = priceElement.Text().ParseToInt();
                
                if (data.InitialPrice == null)
                {
                    data.LastPrice = price;
                    data.InitialPrice = price;
                }
                else if (data.LastPrice != data.InitialPrice && data.LastPrice != price)
                {
                    data.LastPrice = price;
                    Debug.Assert(data.LastPrice.HasValue && data.InitialPrice.HasValue);
                    data.PriceDifference = decimal.Round(100u * (data.LastPrice.Value - data.InitialPrice.Value) / data.InitialPrice.Value , 1);
                    needDetails = true;
                }
            }

            // get details url
            var detailsElement = element.Get<IHtmlAnchorElement>("a", anchor => anchor.Dataset["go-to-expose-id"] == objectId).First();

            // get image url
            try
            {
                var url = detailsElement.QuerySelector<IHtmlElement>("img")?.Dataset["lazy-src"];
                if (url != null)
                {
                    data.ImageFileName = LoadImage(new Uri(url), this.dataPath, id);
                }
            }
            catch (Exception)
            {
                Trace.WriteLine($"Failed to load image for {id}");
            }

            if (needDetails)
            {
                // Distance
                try
                {
                    data.Distance = decimal.Parse(element.Get("div", div => div.ClassList.Contains("result-list-entry__data")).First()
                        .Get("div", div => div.ClassList.Contains("nine-tenths")).First().FirstChild.Text().Split(" ")[0], CultureInfo.InvariantCulture);
                }
                catch
                {
                    // ignored
                }

                Trace.WriteLine($"Reading details for {detailsElement.Href}");
                data.Url = detailsElement.Href;
                ParseDetails(new Url(detailsElement.Href), data);
            }

            // save data
            data.Save(this.dataPath, id);
        }
    }
}