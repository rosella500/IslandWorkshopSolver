using Dalamud.Logging;
using System;
using System.Collections.Generic;

namespace IslandWorkshopSolver.Solver;

using static RareMaterial;
public enum RareMaterial
{
    None=0,
    Fleece=52,
    Claw=53,
    Fur=54,
    Egg=56,
    Carapace=57,
    Fang=58,
    Milk=60,
    Feather=55,
    Horn=59,
    Garnet=28,
    Alyssum=27,
    Spruce=29,
    Shark=30,
    Silver=31,
    Cabbage=43,
    Pumpkin=45,
    Parsnip=50,
    Popoto=42,
    Isleberry=44,
    Tomato=47,
    Onion=46,
    Wheat=48,
    Corn=49,
    Radish=51
}

public static class RareMaterialHelper
{
    public static Dictionary<RareMaterial, int> materialValues = new Dictionary<RareMaterial, int>();
    public static void Init(List<int> values)
    {
        materialValues.Clear();
        for (int i = 0; i < values.Count; i++)
        {
            materialValues.Add((RareMaterial)i, values[i]);
        }
    }

    public static void InitFromGameData(Dictionary<byte,ushort> matData)
    {
        materialValues.Clear();
        materialValues.Add(None, 0);
        foreach(var kvp in matData)
        {
            if (Enum.IsDefined((RareMaterial)kvp.Key))
                materialValues.Add((RareMaterial)kvp.Key, kvp.Value);
        }
        foreach (var kvp in materialValues)
            PluginLog.Debug("Mat: {0}, Value: {1}", kvp.Key, kvp.Value);
    }

    public static void DefaultInit()
    {
        materialValues.Clear();
        materialValues.Add(None, 0);
        materialValues.Add(Fleece, 12);
        materialValues.Add(Claw, 12);
        materialValues.Add(Fur, 12);
        materialValues.Add(Egg, 12);
        materialValues.Add(Carapace, 12);
        materialValues.Add(Fang, 12);
        materialValues.Add(Milk, 12);
        materialValues.Add(Feather, 12);
        materialValues.Add(Horn, 12);
        materialValues.Add(Garnet, 25);
        materialValues.Add(Alyssum, 25);
        materialValues.Add(Spruce, 25);
        materialValues.Add(Shark, 25);
        materialValues.Add(Silver, 25);
        materialValues.Add(Cabbage, 4);
        materialValues.Add(Pumpkin, 4);
        materialValues.Add(Parsnip, 5);
        materialValues.Add(Popoto, 5);
        materialValues.Add(Isleberry, 6);
        materialValues.Add(Tomato, 6);
        materialValues.Add(Onion, 6);
        materialValues.Add(Wheat, 6);
        materialValues.Add(Corn, 6);
        materialValues.Add(Radish, 6);
    }
}
