using ImmoReader;
using System;
using System.Collections.Generic;

public static class ActiveHousesWriter
{
    public static void Save(IList<ImmoData> data)
    {
        while(data.Count > 0)
        {
            var checkingData = data[0];
            data.RemoveAt(0);

            var similar = new List<int>();
            for (int idx = 0; idx < data.Count; idx++)
            {
                if(IsSimilar(checkingData, data[idx]))
                {
                    similar.Add(idx);
                }
            }

            similar.Sort();

            for (int idx = similar.Count - 1; idx >= 0; idx--)
            {
                data.RemoveAt(similar[idx]);
            }

            Save(checkingData);
        }
    }

    public static void Save(ImmoData data)
    {
        Helper.SaveActive(data);
    }

    static bool IsSimilar(ImmoData data1, ImmoData data2)
    {
        if(data1.Id == data2.Id)
        {
            return true;
        }

        if( data1.Year == data2.Year &&
            data1.SiteArea == data2.SiteArea &&
            data1.LivingArea == data2.LivingArea)
        {
            return true;
        }

        return false;
    }
}
