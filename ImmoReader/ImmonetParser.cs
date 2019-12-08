using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImmoReader
{
    class ImmonetParser : IParser
    {
        private string dataPath;

        internal ImmonetParser(string dataPath)
        {
            this.dataPath = dataPath;
        }

        private const string idPrefix = "selObject_";

        public string Type => ImmoPageType.Immonet.ToString();

        public int GetCount(IDocument document)
        {
            return int.Parse(Regex.Match(document.QuerySelectorAll<IHtmlSpanElement>("span").Where(span => span.Id == "totalCount").First().Text().Trim(), @"\d+").Value);
        }

        public string Parse(IDocument document, out int count)
        {
            // Parse all entries
            var elements = document.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div.Id?.StartsWith(idPrefix) ?? false);
            Parallel.ForEach(elements, divElement => {
                try
                {
                    ParseObject(divElement);
                }
                catch
                {
                    Console.WriteLine($"Failed to parse {divElement.Id.Remove(0, idPrefix.Length)}");
                }
                
                
        });

            count = elements.Count();
            return FindNextPage(document);
        }

        private string FindNextPage(IDocument document)
        {
            var result = document.QuerySelectorAll<IHtmlAnchorElement>("a").Where(anchor => anchor.ClassList.Contains(new string[] { "pull-right", "text-right" })).ToList();

            if (result.Count == 0) { return null; }
            if (result.Count == 1)
            { return result[0].Href; }

            throw new ArgumentException("More than one Next page entries found");
        }

        private void ParseObject(IHtmlDivElement divElement)
        {
            var id = divElement.Id.Remove(0, idPrefix.Length);
            var idPath = Path.Combine(dataPath, id);
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

            // Title, Location and Type
            var descriptionParts = divElement.QuerySelectorAll<IHtmlSpanElement>("span").Where(span => span.ClassList.Contains("text-100")).First().Text().Split("•");

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

            if (results.Count == 0) { return; }
            else if (results.Count == 1)
            {
                var detailsElement = results[0];
                Console.WriteLine($"Reading {detailsElement.Href}");

                // get image url
                try
                {
                    data.ImageFileName = LoadImage(new Uri(detailsElement.QuerySelector<IHtmlImageElement>("img").Dataset["original"]), idPath);
                }
                catch (Exception)
                {
                    Console.WriteLine($"Failed to load image for {id}");
                }

                
                // get details from details page
                ParseDetails(detailsElement.Href, data);
            }
            else { throw new ArgumentException("More than one link to details found"); }
            
            // save data
            File.WriteAllText(idFilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private string LoadImage(Uri imageUrl, string dataPath)
        {
            var fileName = imageUrl.Segments.Last();
            var imagePath = Path.Combine(dataPath, fileName);

            if (!File.Exists(imagePath))
            {
                HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(imageUrl);
                HttpWebResponse httpWebReponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (Stream stream = httpWebReponse.GetResponseStream())

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
            data.LastDate = DateTime.Today;

            var priceElement = detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div.Id == "priceid_1").FirstOrDefault();
            if (priceElement != null) {
                data.LastPrice = int.Parse(Regex.Match(priceElement.Text(), @"\d+").Value);

                if (data.InitialPrice == null)
                {
                    data.InitialPrice = data.LastPrice;
                }
            }

            // broker
            data.Broker = detailsDocument.QuerySelectorAll<IHtmlParagraphElement>("p").Where(div => div?.Id == "bdlName").FirstOrDefault()?.Text().Trim();

            data.BrokerFirm = detailsDocument.QuerySelectorAll<IHtmlParagraphElement>("p").Where(div => div?.Id == "bdlFirmname").FirstOrDefault()?.Text().Trim();

            // living size
            data.LivingSize = int.Parse(Regex.Match(detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div?.Id == "areaid_1").First().Text().Trim(), @"\d+").Value);

            // living size
            data.GroundSize = int.Parse(Regex.Match(detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div?.Id == "areaid_3").First().Text().Trim(), @"\d+").Value);

            // room number
            var roomsElement = detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div?.Id == "equipmentid_1").FirstOrDefault();
            if (roomsElement != null)
            {
                data.RoomCount = int.Parse(Regex.Match(roomsElement.Text().Trim(), @"\d+").Value);
            }

            // Title
            data.Title = detailsDocument.QuerySelectorAll<IHtmlElement>("h1").Where(div => div?.Id == "expose-headline").FirstOrDefault()?.Text().Trim();

            // Year
            if(int.TryParse(Regex.Match(detailsDocument.QuerySelectorAll<IHtmlDivElement>("div").Where(div => div?.Id == "yearbuild").FirstOrDefault()?.Text().Trim() ?? string.Empty, @"\d+").Value, out var year))
            {
                data.Year = year;
            }

            
        }
    }
}
