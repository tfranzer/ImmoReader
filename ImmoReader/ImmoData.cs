namespace ImmoReader
{
    using System;
    using System.Collections.Generic;

    public class ImmoData
    {
        public string Id { get; set; }

        public string ImageFileName { get; set; }

        public DateTime? OnlineSince { get; set; }

        public DateTime? FirstSeenDate { get; set; }

        public DateTime? LastSeenDate { get; set; }

        public int? InitialPrice { get; set; }

        public int? LastPrice { get; set; }

        public decimal? PriceDifference { get; set; }

        public string Realtor { get; set; }

        public string RealtorCompany { get; set; }

        public int? LivingArea { get; set; }

        public int? SiteArea { get; set; }

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