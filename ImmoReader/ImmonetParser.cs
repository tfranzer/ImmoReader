namespace ImmoReader
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;

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
            return document.Get("span", span => span.Id == "totalCount").First().Text().ParseToInt().GetValueOrDefault(0);
        }

        public Url Parse(IDocument document, out IList<ImmoData> readData)
        {
            // Parse all entries
            var elements = document.Get("div", div => div.Id?.StartsWith(idPrefix) ?? false);
            var read = new List<ImmoData>();
            Parallel.ForEach(
                elements,
                element =>
                    {
                        try
                        {
                            var data = this.ParseObject(element);

                            lock (this)
                            {
                                read.Add(data);
                            }
                        }
                        catch
                        {
                            Trace.WriteLine($"Failed to parse {element.Id.Remove(0, idPrefix.Length)}");
                        }
                    });

            readData = read;
            return FindNextPage(document);
        }

        private static Url FindNextPage(IDocument document)
        {
            var result = document.Get<IHtmlAnchorElement>("a", anchor => anchor.ClassList.Contains(new[] { "pull-right", "text-right" }));

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

        private static string LoadImage(Uri imageUrl, string dataPath, string id)
        {
            var fileName = $"{id}{new FileInfo(imageUrl.Segments.Last()).Extension}";

            Helper.LoadImage(imageUrl, Path.Combine(dataPath, fileName));

            return fileName;
        }

        private static void ParseDetails(Url url, ImmoData data)
        {
            var detailsDocument = url.Open();

            // broker
            data.Realtor = detailsDocument.Get("p", div => div?.Id == "bdlName").FirstOrDefault()?.Text().Trim();
            data.RealtorCompany = detailsDocument.Get("p", div => div?.Id == "bdlFirmname").FirstOrDefault()?.Text().Trim();

            // don't parse data for Zwangsversteigerung
            if (data.RealtorCompany?.Contains("Zwangsversteigerung") ?? false)
            {
                return;
            }

            Trace.WriteLine($"Reading details for {data.Url}");

            // Title
            data.Title = detailsDocument.Title;

            var fullText = detailsDocument.All[0].Text();

            // Location
            var locationObject = Helper.ReadJson(fullText, "geoLocation:", endToken:"}");
            if (locationObject != null)
            {
                var lat = locationObject["lat"].ToString();
                var lon = locationObject["lng"].ToString();
                data.LocationUrl = $"https://www.google.com/maps/search/{lat},{lon}";
            }

            // living area
            var livingAreaElement = detailsDocument.Get("div", div => div?.Id == "areaid_1").FirstOrDefault();
            if (livingAreaElement != null)
            {
                data.LivingArea = detailsDocument.Get("div", div => div?.Id == "areaid_1").First().Text().ParseToInt(false);
            }

            // site area
            data.SiteArea = detailsDocument.Get("div", div => div?.Id == "areaid_3").FirstOrDefault()?.Text().ParseToInt(false);

            // room number
            var roomsElement = detailsDocument.Get("div",div => div?.Id == "equipmentid_1").FirstOrDefault();
            if (roomsElement != null)
            {
                data.RoomCount = int.Parse(Regex.Match(roomsElement.Text().Trim(), @"\d+").Value);
            }

            // Year
            data.Year = detailsDocument.Get("div", div => div?.Id == "yearbuild").FirstOrDefault()?.Text().ParseToInt();
        }

        private ImmoData ParseObject(IHtmlElement element)
        {
            var objectId = element.Id.Remove(0, idPrefix.Length);
            var id = $"{this.Type}-{objectId}";
            
            var data = Helper.GetImmoData(this.dataPath, id);
            var needDetails = string.IsNullOrEmpty(data.Url);

            // Date and price
            data.LastSeenDate = DateTime.Today;

            var priceElement = element.Get("div", div => div.Id == $"selPrice_{objectId}").FirstOrDefault()?.Children[1];
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
                    data.PriceDifference = decimal.Round(100u * (data.LastPrice.Value - data.InitialPrice.Value) / data.InitialPrice.Value, 1);
                    needDetails = true;
                }
            }

            // get details url
            var detailsElement = element.Get<IHtmlAnchorElement>("a", anchor => anchor.Id?.StartsWith("lnkImgToDetails_") ?? false).First();

            // get image url
            try
            {
                var url = detailsElement.QuerySelector<IHtmlElement>("img")?.Dataset["original"];
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
                // Distance, Location and Type
                var descriptionParts = element.Get("span",span => span.ClassList.Contains("text-100")).First().Text().Split("•");

                if (descriptionParts.Length == 3)
                {
                    data.Location = descriptionParts[2].Trim().Replace("\n\t\t\n\t\t\n\t\t\t", " ");
                    data.Type = descriptionParts[1].Trim();
                    data.Distance = decimal.Round(decimal.Parse(descriptionParts[0].Trim().Replace("km", string.Empty).Trim(), CultureInfo.InvariantCulture), 2);
                }

                // Tags
                data.Tags = element.Get("span", span => span.ClassList.Contains("tag-element-50")).Select(span => span.Text()).ToHashSet();

                // go to details page
                data.Url = detailsElement.Href;
                ParseDetails(new Url(detailsElement.Href), data);
            }

            // save data
            data.Save(this.dataPath, id);

            return data;
        }
    }
}