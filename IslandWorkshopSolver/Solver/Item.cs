namespace IslandWorkshopSolver.Solver;
using static Item;
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
    Crook
}

public static class ItemHelper
{
    public static string GetDisplayName(Item item)
    {
        switch(item)
        {
            case Potion: return "Potion";
            case Firesand: return "Firesand";
            case WoodenChair: return "Wooden Chair";
            case GrilledClam: return "Grilled Clam";
            case Necklace: return "Necklace";
            case CoralRing: return "Coral Ring";
            case Barbut: return "Barbut";
            case Macuahuitl: return "Macuahuitl";
            case Sauerkraut: return "Sauerkraut";
            case BakedPumpkin: return "Baked Pumpkin";
            case Tunic: return "Tunic";
            case CulinaryKnife: return "Culinary Knife";
            case Brush: return "Brush";
            case BoiledEgg: return "Boiled Egg";
            case Hora: return "Hora";
            case Earrings: return "Earrings";
            case Butter: return "Butter";
            case BrickCounter: return "Brick Counter";
            case BronzeSheep: return "Bronze Sheep";
            case GrowthFormula: return "Growth Formula";
            case GarnetRapier: return "Garnet Rapier";
            case SpruceRoundShield: return "Spruce Round Shield";
            case SharkOil: return "Shark Oil";
            case SilverEarCuffs: return "Silver Ear Cuffs";
            case SweetPopoto: return "Sweet Popoto";
            case ParsnipSalad: return "Parsnip Salad";
            case Caramels: return "Caramels";
            case Ribbon: return "Ribbon";
            case Rope: return "Rope";
            case CavaliersHat: return "Cavalier's Hat";
            case Horn: return "Horn";
            case SaltCod: return "Salt Cod";
            case SquidInk: return "Squid Ink";
            case EssentialDraught: return "Essential Draught";
            case Jam: return "Isleberry Jam";
            case TomatoRelish: return "Tomato Relish";
            case OnionSoup: return "Onion Soup";
            case Pie: return "Islefish Pie";
            case CornFlakes: return "Corn Flakes";
            case PickledRadish: return "Pickled Radish";
            case IronAxe: return "Iron Axe";
            case QuartzRing: return "Quartz Ring";
            case PorcelainVase: return "Porcelain Vase";
            case VegetableJuice: return "Vegetable Juice";
            case PumpkinPudding: return "Pumpkin Pudding";
            case SheepfluffRug: return "Sheepfluff Rug";
            case GardenScythe: return "Garden Scythe";
            case Bed: return "Bed";
            case ScaleFingers: return "Scale Fingers";
            case Crook: return "Crook";
            default: return "Unknown";
        }
    }
}
