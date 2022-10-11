namespace IslandWorkshopSolver.Solver;

using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using static Item;
using static Lumina.Data.Parsing.Uld.UldRoot;

public enum Item
{
    Potion = 1,
    Firesand = 2,
    WoodenChair = 3,
    GrilledClam = 4,
    Necklace = 5,
    CoralRing = 6,
    Barbut = 7,
    Macuahuitl = 8,
    Sauerkraut = 9,
    BakedPumpkin = 10,
    Tunic = 11,
    CulinaryKnife = 12,
    Brush =13,
    BoiledEgg =14,
    Hora=15,
    Earrings=16,
    Butter=17,
    BrickCounter=18,
    BronzeSheep=19,
    GrowthFormula=20,
    GarnetRapier=21,
    SpruceRoundShield=22,
    SharkOil=23,
    SilverEarCuffs=24,
    SweetPopoto=25,
    ParsnipSalad=26,
    Caramels=27,
    Ribbon=28,
    Rope=29,
    CavaliersHat=30,
    Horn=31,
    SaltCod=32,
    SquidInk=33,
    EssentialDraught=34,
    Jam=35,
    TomatoRelish=36,
    OnionSoup=37,
    Pie=38,
    CornFlakes=39,
    PickledRadish=40,
    IronAxe=41,
    QuartzRing=42,
    PorcelainVase=43,
    VegetableJuice=44,
    PumpkinPudding=45,
    SheepfluffRug=46,
    GardenScythe=47,
    Bed=48,
    ScaleFingers=49,
    Crook=50 //Update InitFromGameData to be the last item if we add more
}

public static class ItemHelper
{
    private static Dictionary<Item, string> displayNames = new Dictionary<Item, string>();

    public static void DefaultInit()
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
    }
    public static string GetDisplayName(Item item)
    {
        if (displayNames.ContainsKey(item))
            return displayNames[item];
        else
            return "???";
    }

    public static void InitFromGameData(IReadOnlyList<string> itemData)
    {
        for (int i= 0; i<(int)Crook; i++) //Update this to be the last item
        {
            if (Enum.IsDefined((Item)i))
                displayNames.Add((Item)i, itemData[i]);
        }
        foreach (var kvp in displayNames)
            PluginLog.Debug("Item: {0}, Display: {1}", kvp.Key, kvp.Value);
    }
}
