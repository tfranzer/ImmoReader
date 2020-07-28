namespace ImmoReader
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Data.SQLite;
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
        internal static SQLiteConnection Connection { get; set; }
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

        internal static int? ParseToInt(this string text, bool removeDots = true)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            if (removeDots)
            {
                text = text.Replace(".", string.Empty);
            }

            if(int.TryParse(Regex.Match(text.Trim(), @"\d+").Value, out var value))
            {
                return value;
            }

            return null;
        }

        internal static ImmoData GetImmoData(string folder, string id)
        {
            var filePath = Path.Combine(folder, $"{id}.json");

            ImmoData data = null;
            if (File.Exists(filePath))
            {
                data = JsonConvert.DeserializeObject<ImmoData>(File.ReadAllText(filePath));
            }

            return data ?? new ImmoData { Id = id, FirstSeenDate = DateTime.Today };
        }

        internal static (string Id, int Price)[] FindSimilar(int year, int siteAreaMin, int siteAreaMax, int livingAreaMin, int livingAreaMax)
        {
            const string sql = "select [id], [initalprice] from [houses] where [year] = @year and [sitearea] between @siteAreaMin and @siteAreaMax and [livingarea] between @livingAreaMin and @livingAreaMax";
            using var command = new SQLiteCommand(sql, Connection);
            command.Parameters.Add(new SQLiteParameter("@year", year));
            command.Parameters.Add(new SQLiteParameter("@siteAreaMin", siteAreaMin));
            command.Parameters.Add(new SQLiteParameter("@siteAreaMax", siteAreaMax));
            command.Parameters.Add(new SQLiteParameter("@livingAreaMin", livingAreaMin));
            command.Parameters.Add(new SQLiteParameter("@livingAreaMax", livingAreaMax));

            var result = new List<(string Id, int Price)>();
            var reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    var v1 = reader.GetValue(1);
                    if (v1 is DBNull)
                    {
                        continue;
                    }
                    
                    result.Add((reader.GetString(0), reader.GetInt32(1)));
                }
            }

            return result.ToArray();
        }

        internal static void Save(this ImmoData data, string folder, string id)
        {
            // don't save data for Zwangsversteigerung
            if (data.RealtorCompany?.Contains("Zwangsversteigerung") ?? false)
            {
                return;
            }

            File.WriteAllText(Path.Combine(folder, $"{id}.json"), JsonConvert.SerializeObject(data, Formatting.Indented));

            // write to db
            using (var cmd = new SQLiteCommand(Helper.Connection))
            {
                cmd.CommandText = "INSERT OR REPLACE INTO houses Values(" +
                    "@id," +
                    "@location," +
                    "@distance," +
                    "@price," +
                    "@initalprice," +
                    "@pricediff," +
                    "@onlinesince," +
                    "@firstseen," +
                    "@lastseen," +
                    "@type," +
                    "@title," +
                    "@livingarea," +
                    "@sitearea," +
                    "@rooms," +
                    "@year," +
                    "@realtor," +
                    "@realtorcompany," +
                    "@url," +
                    "@locationurl," +
                    "@image," +
                    "@tags)";
                    

                cmd.Parameters.Add(new SQLiteParameter("@id", id));
                cmd.Parameters.Add(new SQLiteParameter("@image", data.ImageFileName));
                cmd.Parameters.Add(new SQLiteParameter("@onlinesince", data.OnlineSince));
                cmd.Parameters.Add(new SQLiteParameter("@firstseen", data.FirstSeenDate));
                cmd.Parameters.Add(new SQLiteParameter("@lastseen", data.LastSeenDate));
                cmd.Parameters.Add(new SQLiteParameter("@initalprice", data.InitialPrice));
                cmd.Parameters.Add(new SQLiteParameter("@pricediff", data.PriceDifference));
                cmd.Parameters.Add(new SQLiteParameter("@price", data.LastPrice));
                cmd.Parameters.Add(new SQLiteParameter("@realtor", data.Realtor));
                cmd.Parameters.Add(new SQLiteParameter("@realtorcompany", data.RealtorCompany));
                cmd.Parameters.Add(new SQLiteParameter("@livingarea", data.LivingArea));
                cmd.Parameters.Add(new SQLiteParameter("@sitearea", data.SiteArea));
                cmd.Parameters.Add(new SQLiteParameter("@rooms", data.RoomCount));
                cmd.Parameters.Add(new SQLiteParameter("@year", data.Year));
                cmd.Parameters.Add(new SQLiteParameter("@distance", data.Distance));
                cmd.Parameters.Add(new SQLiteParameter("@location", data.Location));
                cmd.Parameters.Add(new SQLiteParameter("@type", data.Type));
                cmd.Parameters.Add(new SQLiteParameter("@title", data.Title));
                cmd.Parameters.Add(new SQLiteParameter("@url", data.Url));
                cmd.Parameters.Add(new SQLiteParameter("@locationurl", data.LocationUrl));
                cmd.Parameters.Add(new SQLiteParameter("@tags", string.Join(';', data.Tags ?? Enumerable.Empty<string>())));

                cmd.ExecuteNonQuery();
            }
        }

        internal static void SaveActive(this ImmoData data, string comment)
        {
            // don't save data for Zwangsversteigerung
            if (data.RealtorCompany?.Contains("Zwangsversteigerung") ?? false)
            {
                return;
            }

            // write to db
            using (var cmd = new SQLiteCommand(Helper.Connection))
            {
                cmd.CommandText = "INSERT OR REPLACE INTO active_houses Values(" +
                    "@id," +
                    "@url," +
                    "@location," +
                    "@price," +
                    "@initalprice," +
                    "@pricediff," +
                    "@onlinesince," +
                    "@firstseen," +
                    "@title," +
                    "@livingarea," +
                    "@sitearea," +
                    "@year," +
                    "@comment," +
                    "@realtorcompany)";


                cmd.Parameters.Add(new SQLiteParameter("@id", data.Id));
                cmd.Parameters.Add(new SQLiteParameter("@url", data.Url));
                cmd.Parameters.Add(new SQLiteParameter("@location", data.Location));
                cmd.Parameters.Add(new SQLiteParameter("@price", data.LastPrice));
                cmd.Parameters.Add(new SQLiteParameter("@initalprice", data.InitialPrice));
                cmd.Parameters.Add(new SQLiteParameter("@pricediff", data.PriceDifference));
                cmd.Parameters.Add(new SQLiteParameter("@onlinesince", data.OnlineSince));
                cmd.Parameters.Add(new SQLiteParameter("@firstseen", data.FirstSeenDate));
                cmd.Parameters.Add(new SQLiteParameter("@title", data.Title));
                cmd.Parameters.Add(new SQLiteParameter("@livingarea", data.LivingArea));
                cmd.Parameters.Add(new SQLiteParameter("@sitearea", data.SiteArea));
                cmd.Parameters.Add(new SQLiteParameter("@year", data.Year));
                cmd.Parameters.Add(new SQLiteParameter("@comment", comment));
                cmd.Parameters.Add(new SQLiteParameter("@realtorcompany", data.RealtorCompany));
                
                cmd.ExecuteNonQuery();
            }
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