namespace ImmoReader
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;

    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class Helper
    {
        internal static IDocument Open(this Url url)
        {
            return BrowsingContext.New(AngleSharp.Configuration.Default.WithDefaultLoader()).OpenAsync(url).Result;
        }

        internal static IList<IHtmlElement> Get(this IParentNode document, string selector, Func<IHtmlElement, bool> filterFunc)
        {
            return document.Get<IHtmlElement>(selector, filterFunc);
        }

        internal static IList<T> Get<T>(this IParentNode document, string selector, Func<T, bool> filterFunc)
            where T : IHtmlElement
        {
            return document.QuerySelectorAll<T>(selector).Where(filterFunc.Invoke).ToList();
        }

        internal static int ParseToInt(this string text)
        {
            return int.Parse(Regex.Match(text.Replace(".", string.Empty).Trim(), @"\d+").Value);
        }

        internal static ImmoData GetImmoData(string folder, string id)
        {
            var filePath = Path.Combine(folder, $"{id}.json");
            if (File.Exists(filePath))
            {
                return JsonConvert.DeserializeObject<ImmoData>(File.ReadAllText(filePath));
            }

            return new ImmoData { Id = id, FirstSeenDate = DateTime.Today };
        }

        internal static void Save(this ImmoData data, string folder, string id)
        {
            File.WriteAllText(Path.Combine(folder, $"{id}.json"), JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        internal static JObject ReadJson(string content, string token, string startToken = "{", string endToken = "};")
        {
            try
            {
                var idx = content.IndexOf(token, StringComparison.InvariantCulture);
                if (idx == -1)
                {
                    return null;
                }

                var idxStart = content.IndexOf(startToken, idx + token.Length, StringComparison.InvariantCulture);
                var idxEnd = content.IndexOf(endToken, idx, StringComparison.InvariantCulture);
                return (JObject)JsonConvert.DeserializeObject(content.Substring(idxStart, idxEnd - idxStart + 1));
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static void LoadImage(Uri imageUrl, string imagePath)
        {
            if (File.Exists(imagePath))
            {
                return;
            }

            var request = WebRequest.Create(imageUrl);
            var response = request.GetResponse();
            using (var stream = response.GetResponseStream())
            using (var fileStream = File.Create(imagePath))
            {
                stream?.CopyTo(fileStream);
            }
        }
    }
}