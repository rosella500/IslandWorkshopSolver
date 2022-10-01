using System.Collections.Generic;

namespace IslandWorkshopSolver.Solver;

using static RareMaterial;
public enum RareMaterial
{
    None,
    Fleece,
    Claw,
    Fur,
    Egg,
    Carapace,
    Fang,
    Milk,
    Feather,
    Horn,
    Garnet,
    Alyssum,
    Spruce,
    Shark,
    Silver,
    Cabbage,
    Pumpkin,
    Parsnip,
    Popoto,
    Isleberry,
    Tomato,
    Onion,
    Wheat,
    Corn,
    Radish
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
