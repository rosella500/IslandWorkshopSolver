using Dalamud.Logging;
using System;
using System.Collections.Generic;

namespace IslandWorkshopSolver.Solver;
using static Popularity;
public enum Popularity
{
    VeryHigh=1, High=2, Average=3, Low=4
};

public static class PopularityHelper
{
    private static Dictionary<Popularity, int> popModifiers = new Dictionary<Popularity, int>();
    public static void Init(List<int> popMods)
    {
        popModifiers.Clear();

        for (int i = 0; i < popMods.Count; i++)
        {
            popModifiers.Add((Popularity)i, popMods[i]);
        }
    }


    public static int GetMultiplier(Popularity pop)
    {
        return popModifiers[pop];
    }


    public static void DefaultInit()
    {
        popModifiers.Clear();
        popModifiers.Add(Low, 80);
        popModifiers.Add(Average, 100);
        popModifiers.Add(High, 120);
        popModifiers.Add(VeryHigh, 140);
    }

    public static void InitFromGameData(Dictionary<uint, ushort> popData)
    {
        popModifiers.Clear();
        foreach (var kvp in popData)
        {
            if(Enum.IsDefined((Popularity)kvp.Key))
                popModifiers.Add((Popularity)kvp.Key, kvp.Value);
        }
        foreach (var kvp in popModifiers)
            PluginLog.Verbose("Popularity: {0}, Modifier: {1}", kvp.Key, kvp.Value);
    }
}
