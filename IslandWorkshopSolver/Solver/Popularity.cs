using System.Collections.Generic;

namespace IslandWorkshopSolver.Solver;
using static Popularity;
public enum Popularity
{
    VeryHigh, High, Average, Low
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
}
