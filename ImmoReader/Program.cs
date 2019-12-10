namespace ImmoReader
{
    using System;
    using System.Data.SQLite;
    using System.IO;
    using System.Threading.Tasks;

    using AngleSharp;

    using Newtonsoft.Json;

    internal class Program
    {
        private static void Main(string[] args)
        {
            var configPath = new DirectoryInfo(args.Length == 1 ? args[0] : @"..\..\..\Misc\config.json");
            var config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(configPath.FullName));
            var dataPath = new DirectoryInfo(config.DataPath).FullName;

            using (SQLiteConnection connection = InitDB(dataPath))
            {
                Parallel.ForEach(
                    config.EntryPages,
                    entry =>
                        {
                            var (immoPageType, urls) = entry;
                            var reader = new HtmlReader(dataPath, immoPageType);

                            Parallel.ForEach(
                                urls,
                                url =>
                                    {
                                        try
                                        {
                                            reader.Read(new Url(url));
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(e);
                                        }
                                    });
                        });
            }
        }

        private static SQLiteConnection InitDB(string dataPath)
        {
            var con = new SQLiteConnection($@"URI=file:{Path.Combine(dataPath, "data.db")}");
            con.Open();

            using (var cmd = new SQLiteCommand(con))
            {
                //cmd.CommandText = "DROP TABLE IF EXISTS houses";
                //cmd.ExecuteNonQuery();

                cmd.CommandText = @"CREATE TABLE if not exists houses("+
                        "id TEXT PRIMARY KEY,"+
                        "location TEXT," +
                        "distance REAL," +
                        "price INT," +
                        "initalprice INT," +
                        "onlinesince TEXT,"+
                        "firstseen TEXT,"+
                        "lastseen TEXT,"+
                        "type TEXT," +
                        "title TEXT," +
                        "livingarea INT," +
                        "sitearea INT," +
                        "rooms INT," +
                        "year INT," +
                        "realtor TEXT," +
                        "realtorcompany TEXT,"+
                        "url TEXT,"+
                        "locationurl TEXT,"+
                        "image TEXT," +
                        "tags TEXT)";
                cmd.ExecuteNonQuery();
            }

            Helper.Connection = con;
            return con;
        }
    }
}