using Dalamud.Logging;
using System;
using System.Collections.Generic;

namespace Beachcomber.Solver;
using static Supply;
public enum Supply
{
    Nonexistent,
    Insufficient,
    Sufficient,
    Surplus,
    Overflowing
}

public static class SupplyHelper
{
    private static readonly Dictionary<Supply, int> supplyModifiers = new Dictionary<Supply, int>();
    public static void Init(List<int> supplyMods)
    {
        supplyModifiers.Clear();

        for (int i = 0; i < supplyMods.Count; i++)
        {
            supplyModifiers.Add((Supply)i, supplyMods[i]);
        }
    }

    public static int GetMultiplier(Supply supply)
    {
        return supplyModifiers[supply];
    }

/*    public static void DefaultInit()
    {
        supplyModifiers.Clear();
        supplyModifiers.Add(Overflowing, 60);
        supplyModifiers.Add(Surplus, 80);
        supplyModifiers.Add(Sufficient, 100);
        supplyModifiers.Add(Insufficient, 130);
        supplyModifiers.Add(Nonexistent, 160);
    }*/

    public static void InitFromGameData(Dictionary<uint, ushort> supplyData)
    {
        supplyModifiers.Clear();
        foreach(var kvp in supplyData)
        {
            if (Enum.IsDefined((Supply)kvp.Key))
                supplyModifiers.Add((Supply)kvp.Key, kvp.Value);
        }
        foreach (var kvp in supplyModifiers)
            PluginLog.Verbose("Supply: {0}, Modifier: {1}", kvp.Key, kvp.Value);
    }
}



