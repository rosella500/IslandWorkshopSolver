namespace IslandWorkshopSolver.Solver;

using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using static Item;
using static Lumina.Data.Parsing.Uld.UldRoot;

public enum Item
{
    Potion,
    Firesand,
    WoodenChair,
    GrilledClam,
    Necklace,
    CoralRing,
    Barbut,
    Macuahuitl,
    Sauerkraut,
    BakedPumpkin,
    Tunic,
    CulinaryKnife,
    Brush,
    BoiledEgg,
    Hora,
    Earrings,
    Butter,
    BrickCounter,
    BronzeSheep,
    GrowthFormula,
    GarnetRapier,
    SpruceRoundShield,
    SharkOil,
    SilverEarCuffs,
    SweetPopoto,
    ParsnipSalad,
    Caramels,
    Ribbon,
    Rope,
    CavaliersHat,
    Horn,
    SaltCod,
    SquidInk,
    EssentialDraught,
    Jam,
    TomatoRelish,
    OnionSoup,
    Pie,
    CornFlakes,
    PickledRadish,
    IronAxe,
    QuartzRing,
    PorcelainVase,
    VegetableJuice,
    PumpkinPudding,
    SheepfluffRug,
    GardenScythe,
    Bed,
    ScaleFingers,
    Crook,
    NumCrafts
}

public static class ItemHelper
{
    private static Dictionary<Item, string> displayNames = new Dictionary<Item, string>();

    /*public static void DefaultInit()
    {
        displayNames.Clear();
        displayNames.Add(Potion, "Potion");
        displayNames.Add(Firesand, "Firesand");
        displayNames.Add(WoodenChair, "Wooden Chair");
        displayNames.Add(GrilledClam, "Grilled Clam");
        displayNames.Add(Necklace, "Necklace");
        displayNames.Add(CoralRing, "Coral Ring");
        displayNames.Add(Barbut, "Barbut");
        displayNames.Add(Macuahuitl, "Macuahuitl");
        displayNames.Add(Sauerkraut, "Sauerkraut");
        displayNames.Add(BakedPumpkin, "Baked Pumpkin");
        displayNames.Add(Tunic, "Tunic");
        displayNames.Add(CulinaryKnife, "Culinary Knife");
        displayNames.Add(Brush, "Brush");
        displayNames.Add(BoiledEgg, "Boiled Egg");
        displayNames.Add(Hora, "Hora");
        displayNames.Add(Earrings, "Earrings");
        displayNames.Add(Butter, "Butter");
        displayNames.Add(BrickCounter, "Brick Counter");
        displayNames.Add(BronzeSheep, "Bronze Sheep");
        displayNames.Add(GrowthFormula, "Growth Formula");
        displayNames.Add(GarnetRapier, "Garnet Rapier");
        displayNames.Add(SpruceRoundShield, "Spruce Round Shield");
        displayNames.Add(SharkOil, "Shark Oil");
        displayNames.Add(SilverEarCuffs, "Silver Ear Cuffs");
        displayNames.Add(SweetPopoto, "Sweet Popoto");
        displayNames.Add(ParsnipSalad, "Parsnip Salad");
        displayNames.Add(Caramels, "Caramels");
        displayNames.Add(Ribbon, "Ribbon");
        displayNames.Add(Rope, "Rope");
        displayNames.Add(CavaliersHat, "Cavalier's Hat");
        displayNames.Add(Horn, "Horn");
        displayNames.Add(SaltCod, "Salt Cod");
        displayNames.Add(SquidInk, "Squid Ink");
        displayNames.Add(EssentialDraught, "Essential Draught");
        displayNames.Add(Jam, "Isleberry Jam");
        displayNames.Add(TomatoRelish, "Tomato Relish");
        displayNames.Add(OnionSoup, "Onion Soup");
        displayNames.Add(Pie, "Islefish Pie");
        displayNames.Add(CornFlakes, "Corn Flakes");
        displayNames.Add(PickledRadish, "Pickled Radish");
        displayNames.Add(IronAxe, "Iron Axe");
        displayNames.Add(QuartzRing, "Quartz Ring");
        displayNames.Add(PorcelainVase, "Porcelain Vase");
        displayNames.Add(VegetableJuice, "Vegetable Juice");
        displayNames.Add(PumpkinPudding, "Pumpkin Pudding");
        displayNames.Add(SheepfluffRug, "Sheepfluff Rug");
        displayNames.Add(GardenScythe, "Garden Scythe");
        displayNames.Add(Bed, "Bed");
        displayNames.Add(ScaleFingers, "Scale Fingers");
        displayNames.Add(Crook, "Crook");
    }*/
    public static string GetDisplayName(Item item)
    {
        if (displayNames.ContainsKey(item))
            return Solver.Config.RemoveFilteredWords(displayNames[item]);
        else
            return "???";
    }

    public static void InitFromGameData(IList<string> itemData)
    {
        for (int i= 0; i<(int)NumCrafts; i++) 
        {
            if (Enum.IsDefined((Item)i))
                displayNames.Add((Item)i, itemData[i]);
        }
        foreach (var kvp in displayNames)
            PluginLog.Verbose("Item: {0}, Display: {1}", kvp.Key, kvp.Value);
    }
}
