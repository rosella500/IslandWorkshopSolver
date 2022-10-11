using Dalamud.Logging;
using System;
using System.Collections.Generic;

namespace IslandWorkshopSolver.Solver;

public enum Material
{
    None = -1,
    PalmLeaf = 0,
    Apple = 1,
    Branch = 2,
    Stone = 3,
    Clam = 4,
    Laver = 5,
    Coral = 6,
    Islewort = 7,
    Sand = 8,
    Log = 9,
    PalmLog = 10,
    Vine = 11,
    Sap = 12,
    CopperOre = 13,
    Limestone = 14,
    RockSalt = 15,
    Sugarcane = 16,
    CottonBoll = 17,
    Hemp = 18,
    Clay = 19,
    Tinsand = 20,
    IronOre = 21,
    Quartz = 22,
    Leucogranite = 23,
    Islefish = 24,
    Squid = 25,
    Jellyfish = 26,
    Alyssum = 27,
    Garnet = 28,
    SpruceLog = 29,
    Hammerhead = 30,
    SilverOre = 31,
    Popoto = 42,
    Cabbage = 43,
    Isleberry = 44,
    Pumpkin = 45,
    Onion = 46,
    Tomato = 47,
    Wheat = 48,
    Corn = 49,
    Parsnip = 50,
    Radish = 51,
    Fleece = 52,
    Claw = 53,
    Fur = 54,
    Feather = 55,
    Egg = 56,
    Carapace = 57,
    Fang = 58,
    Horn = 59,
    Milk = 60
}
public static class RareMaterialHelper
{
    private static Dictionary<Material, int> materialValues = new Dictionary<Material, int>();
    private static Dictionary<Material, string> materialNames = new Dictionary<Material, string>();

    public static void InitFromGameData(Dictionary<byte,ushort> matData, ICollection<string> names)
    {
        materialValues.Clear();
        foreach(var kvp in matData)
        {
            if (Enum.IsDefined((Material)kvp.Key))
                materialValues.Add((Material)kvp.Key, kvp.Value);
        }
        foreach (var kvp in materialValues)
            PluginLog.Verbose("Mat: {0}, Value: {1}", kvp.Key, kvp.Value);

        materialNames.Clear();
        int index = 0;
        foreach (string name in names)
        {
            if (Enum.IsDefined((Material)index))
                materialNames.Add((Material)index, name);

            index++;
        }

        foreach (var kvp in materialNames)
            PluginLog.Verbose("Mat: {0}, Name: {1}", kvp.Key, kvp.Value);
    }

    public static bool GetMaterialValue(Material mat, out int value)
    {
        value = 0;
        if (materialValues.ContainsKey(mat))
        {
            value = materialValues[mat];
            return true;
        }
        else
            return false;
    }

    public static string GetDisplayName(Material mat)
    {
        if (materialNames.ContainsKey(mat))
        {
            return Solver.Config.RemoveFilteredWords(materialNames[mat]);
        }
        else
            return "???";
    }
    /*    public static void DefaultInit()
        {
            materialValues.Clear();
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
        }*/
}
