namespace ImmoReader
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    using AngleSharp;

    using Newtonsoft.Json;

    internal class Program
    {
        private static void Main(string[] args)
        {
            var config = JsonConvert.DeserializeObject<Configuration>(
                File.ReadAllText(Path.Combine(Environment.CurrentDirectory, @"..\..\..\Misc\config.json")));
            var dataPath = new DirectoryInfo(config.DataPath).FullName;

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
}