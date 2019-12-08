using System;
using System.Collections.Generic;
using System.Text;

namespace ImmoReader
{
    public class ImmoData
    {
        public string Id { get; set; }

        public string ImageFileName { get; set; }

        public DateTime InitialDate { get; set; }

        public DateTime LastDate { get; set; }

        public int? InitialPrice { get; set; }

        public int? LastPrice { get; set; }

        public string Broker { get; set; }

        public string BrokerFirm { get; set; }

        public int LivingSize { get; set; }

        public int GroundSize { get; set; }

        public int? RoomCount { get; set; }

        public int? Year { get; set; }

        public decimal? Distance { get; set; }

        public string Location { get; set; }

        public string Type { get; set; }

        public string Title { get; set; }

        public string Url { get; set; }

        public string LocationUrl { get; set; }

        public IEnumerable<string> Tags { get; set; }
    }
}
