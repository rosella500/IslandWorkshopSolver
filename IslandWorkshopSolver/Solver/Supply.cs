using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IslandWorkshopSolver.Solver;
using static Supply;
public enum Supply
{
    Overflowing, Surplus, Sufficient, Insufficient, Nonexistent
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

    public static void DefaultInit()
    {
        supplyModifiers.Clear();
        supplyModifiers.Add(Overflowing, 60);
        supplyModifiers.Add(Surplus, 80);
        supplyModifiers.Add(Sufficient, 100);
        supplyModifiers.Add(Insufficient, 130);
        supplyModifiers.Add(Nonexistent, 160);
    }
}



