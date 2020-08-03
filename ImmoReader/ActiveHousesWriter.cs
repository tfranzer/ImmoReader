using System;
using System.Collections.Generic;
using System.Linq;

using ImmoReader;

public static class ActiveHousesWriter
{
    public static void Save(IList<ImmoData> data)
    {
        while (data.Count > 0)
        {
            var checkingData = data[0];
            data.RemoveAt(0);

            var similar = new List<int>();
            for (var idx = 0; idx < data.Count; idx++)
            {
                if (IsSimilar(checkingData, data[idx]))
                {
                    similar.Add(idx);
                }
            }

            similar.Sort();

            for (var idx = similar.Count - 1; idx >= 0; idx--)
            {
                data.RemoveAt(similar[idx]);
            }

            // find similar in old data
            var siteArea = checkingData.SiteArea.GetValueOrDefault(-1);
            var livingArea = checkingData.LivingArea.GetValueOrDefault(-1);
            var existing = Helper.FindSimilar(checkingData.Year.GetValueOrDefault(-1), siteArea - 2, siteArea + 2, livingArea - 2, livingArea + 2);

            var prices = new List<int> { checkingData.InitialPrice.GetValueOrDefault(-1) };
            prices.AddRange(existing.Select(item => item.Price));
            prices.Sort();

            var firstSeen = new List<DateTime> { checkingData.FirstSeenDate.GetValueOrDefault() };
            firstSeen.AddRange(existing.Select(item => item.FirstSeen));
            firstSeen.Sort();

            checkingData.FirstSeenDate = firstSeen.First();
            checkingData.InitialPrice = prices.LastOrDefault();

            if (checkingData.LastPrice != null && checkingData.InitialPrice != null)
            {
                checkingData.PriceDifference = decimal.Round(100u * (checkingData.LastPrice.Value - checkingData.InitialPrice.Value) / checkingData.InitialPrice.Value, 1);
            }
            checkingData.SaveActive(string.Join(";", existing.Select(item => item.Id)));
        }
    }

    private static bool IsSimilar(ImmoData data1, ImmoData data2)
    {
        if (data1.Id == data2.Id)
        {
            return true;
        }

        if ((data1.Year == data2.Year) && (data1.SiteArea == data2.SiteArea) && (data1.LivingArea == data2.LivingArea))
        {
            return true;
        }

        return false;
    }
}