using System;
using System.Collections.Generic;
using System.Text;

namespace ImmoReader
{
    public enum ImmoPageType
    {
        Immoscout24,
        Immonet
    }

    internal class Configuration
    {
        public Dictionary<ImmoPageType, string[]> EntryPages { get; set; }

        public string DataPath { get; set; }
    }
}
