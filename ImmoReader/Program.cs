namespace ImmoReader
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    internal class Program
    {
        private static void Main(string[] args)
        {
            // Trace.Listeners.Add(new ConsoleTraceListener());

            var config = JsonConvert.DeserializeObject<Configuration>(
                File.ReadAllText(Path.Combine(Environment.CurrentDirectory, @"..\..\..\Misc\config.json")));
            var dataPath = new DirectoryInfo(config.DataPath).FullName;

            Parallel.ForEach(
                config.EntryPages,
                entry =>
                    {
                        var reader = new HtmlReader(dataPath, entry.Key);

                        Parallel.ForEach(
                            entry.Value,
                            url =>
                                {
                                    try
                                    {
                                        reader.Read(url).Wait();
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