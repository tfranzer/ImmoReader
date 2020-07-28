namespace ImmoReader
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    using AngleSharp;

    using Newtonsoft.Json;

    internal class Program
    {
        private static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.Listeners.Add(new TextWriterTraceListener("trace.txt"));
            Trace.AutoFlush = true;

            var configPath = new DirectoryInfo(args.Length == 1 ? args[0] : @"..\..\..\Misc\config.json");
            var config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(configPath.FullName));
            var dataPath = new DirectoryInfo(config.DataPath).FullName;

            Trace.WriteLine($"Started {DateTime.Now}");
            using (SQLiteConnection connection = InitDB(dataPath))
            {
                var allReadData = new List<ImmoData>();

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
                                            var readData = reader.Read(new Url(url));

                                            lock (allReadData)
                                            {
                                                allReadData.AddRange(readData);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Trace.WriteLine(e);
                                        }
                                    });
                        });

                ActiveHousesWriter.Save(allReadData);

            }

            Trace.WriteLine($"Finished {DateTime.Now}");

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
                        "pricediff REAL," +
                        "onlinesince TEXT," +
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

                // active table
                cmd.CommandText = "DROP TABLE IF EXISTS active_houses";
                cmd.ExecuteNonQuery();
                cmd.CommandText = @"CREATE TABLE if not exists active_houses(" +
                        "id TEXT PRIMARY KEY," +
                        "url TEXT," +
                        "location TEXT," +
                        "price INT," +
                        "initalprice INT," +
                        "pricediff REAL," +
                        "onlinesince TEXT," +
                        "firstseen TEXT," +
                        "title TEXT," +
                        "livingarea INT," +
                        "sitearea INT," +
                        "year INT," +
                        "comment TEXT," +
                        "realtorcompany TEXT)";
                cmd.ExecuteNonQuery();
            }

            Helper.Connection = con;
            return con;
        }
    }
}