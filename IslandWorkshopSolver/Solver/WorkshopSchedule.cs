using Dalamud.Logging;
using System.Collections.Generic;
namespace IslandWorkshopSolver.Solver;

public class WorkshopSchedule
{
    private List<ItemInfo> crafts;
    private List<Item> items; //Just a dupe of crafts, but accessible
    private List<int> completionHours;
    public int currentIndex = 0; //Used for cycle scheduler to figure out crafts stuff

    public Dictionary<Material, int> rareMaterialsRequired { get; private set; }

    public WorkshopSchedule(List<Item> crafts)
    {
        completionHours = new List<int>();
        this.crafts = new List<ItemInfo>();
        items = new List<Item>();
        rareMaterialsRequired = new Dictionary<Material, int>();
        SetCrafts(crafts);
    }

    public void SetCrafts(List<Item> newCrafts)
    {
        crafts.Clear();
        newCrafts.ForEach(delegate (Item item) { crafts.Add(Solver.Items[(int)item]); });
        int currentHour = 0;
        completionHours.Clear();
        items.Clear();
        if(newCrafts.Count == 0)
        {
            rareMaterialsRequired.Add(Material.None, 1);
        }
        foreach (ItemInfo craft in crafts)
        {
            currentHour += craft.time;
            items.Add(craft.item);

            completionHours.Add(currentHour);

            foreach (var indivMat in craft.materialsRequired)
            {
                if (!RareMaterialHelper.GetMaterialValue(indivMat.Key, out _)) //If it's not a rare mat, ignore it
                    continue;

                if(rareMaterialsRequired.ContainsKey(indivMat.Key))
                {
                    rareMaterialsRequired[indivMat.Key] += indivMat.Value;
                }
                else
                {
                    rareMaterialsRequired.Add(indivMat.Key, indivMat.Value);
                }
                    
            }
                    
        }

    }

    public ItemInfo? GetCurrentCraft()
    {
        if (currentIndex < crafts.Count)
            return crafts[currentIndex];
        return null;
    }

    public int GetNumCrafts()
    {
        return crafts.Count;
    }

    public bool HasSameCrafts(WorkshopSchedule other)
    {
        if (other.crafts.Count != crafts.Count)
            return false;

        for(int c=0;c<crafts.Count; c++)
        {
            if (crafts[c].item != other.crafts[c].item)
                return false;
        }
        return true;
    }

    public List<Item> GetItems()
    {
        return items;
    }

    public bool CurrentCraftCompleted(int hour)
    {
        if (currentIndex >= crafts.Count)
            return false;

        if (completionHours.Count > 0 && completionHours[currentIndex] == hour)
            return true;
        return false;
    }

    public int GetValueForCurrent(int day, int craftedSoFar, int currentGroove, bool isEfficient)
    {
        ItemInfo craft = crafts[currentIndex];
        int baseValue = craft.baseValue * Solver.WORKSHOP_BONUS * (100 + currentGroove) / 10000;
        int supply = craft.GetSupplyOnDay(day) + craftedSoFar;
        int adjustedValue = baseValue * PopularityHelper.GetMultiplier(craft.popularity) * SupplyHelper.GetMultiplier(ItemInfo.GetSupplyBucket(supply))/ 10000;

        if (isEfficient)
            adjustedValue *= 2;
        //PluginLog.LogVerbose(craft.item + " is worth " + adjustedValue + " with " + currentGroove + " groove at " + ItemInfo.GetSupplyBucket(supply) + " supply (" + supply + ") and " + craft.popularity + " popularity with peak "+craft.peak);

        return adjustedValue;
    }

    public bool CurrentCraftIsEfficient()
    {
        if (currentIndex > 0 && currentIndex < crafts.Count)
            if (crafts[currentIndex].GetsEfficiencyBonus(crafts[currentIndex - 1]))
                return true;

        return false;
    }

    public int GetMaterialCost()
    {
        int cost = 0;
        foreach (ItemInfo craft in crafts)
        {
            cost += craft.materialValue;
        }
        return cost;
    }

    public int GetValueWithGrooveEstimate(int day, int startingGroove)
    {
        int craftsAbove4 = 0;
        craftsAbove4 += GetNumCrafts() - 4;
        int daysToGroove = 5 - day;
        if (!Solver.Rested)
            daysToGroove--;

        int grooveValue = 0;

        if (daysToGroove > 0)
        {
            int fullGrooveBonus = (daysToGroove - 1) * Solver.GroovePerFullDay;
            grooveValue = fullGrooveBonus + Solver.GroovePerPartDay;
            grooveValue *= craftsAbove4;
        }

        int workshopValue = 0;
        Dictionary<Item, int> numCrafted = new Dictionary<Item, int>();
        currentIndex = 0;
        for (int i = 0; i < GetNumCrafts(); i++)
        {
            ItemInfo? completedCraft = GetCurrentCraft();
            if (completedCraft == null)
                continue;
            bool efficient = CurrentCraftIsEfficient();


            int previouslyCrafted = 0;
            if (numCrafted.ContainsKey(completedCraft.item))
            {
                previouslyCrafted = numCrafted[completedCraft.item];
            }
            else
            {
                numCrafted.Add(completedCraft.item, 0);
            }

            workshopValue += GetValueForCurrent(day, previouslyCrafted, startingGroove + i * 3, efficient);
            currentIndex++;
            int amountCrafted = efficient ? 6 : 3;
            numCrafted[completedCraft.item]= previouslyCrafted + amountCrafted;
        }

        //Allow for the accounting for materials if desired
        return grooveValue + workshopValue - (int)(GetMaterialCost() * Solver.MaterialWeight);
    }

    public bool IsSafe(int day)
    {
        foreach(var item in crafts)
        {
            if (!item.PeaksOnOrBeforeDay(day, false))
                return false;
        }
        return true;
    }

    public bool HasAnyUnsurePeaks()
    {
        foreach(ItemInfo itemInfo in crafts)
        {
            if (!itemInfo.peak.IsTerminal())
                return true;
        }
        return false;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as WorkshopSchedule);
    }

    public bool Equals(WorkshopSchedule? other)
    {
        if (other == null)
            return false;

        if (rareMaterialsRequired.Count != other.rareMaterialsRequired.Count) return false;
        foreach (var kvp in rareMaterialsRequired)
        {
            if (!other.rareMaterialsRequired.TryGetValue(kvp.Key, out int neededForOther)) return false;
            if (neededForOther != kvp.Value) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        int hashSum = 0;
        foreach(var rareMat in rareMaterialsRequired)
        {
            hashSum += rareMat.Key.GetHashCode() ^ rareMat.Value.GetHashCode();
        }
        return hashSum;
    }
}
