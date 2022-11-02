using Dalamud.Logging;
using System;
using System.Collections.Generic;
namespace Beachcomber.Solver;

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

    public int GetValueForCurrent(int day, int craftedSoFar, int currentGroove, bool isEfficient, bool verboseLogging = false)
    {
        ItemInfo craft = crafts[currentIndex];
        int baseValue = craft.baseValue * Solver.WORKSHOP_BONUS * (100 + currentGroove) / 10000;
        int supply = craft.GetSupplyOnDay(day) + craftedSoFar;
        int adjustedValue = baseValue * PopularityHelper.GetMultiplier(craft.popularity) * SupplyHelper.GetMultiplier(ItemInfo.GetSupplyBucket(supply))/ 10000;

        if (isEfficient)
            adjustedValue *= 2;
        if(verboseLogging)
            PluginLog.LogDebug(craft.item + " is worth " + adjustedValue + " with " + currentGroove + " groove at " + ItemInfo.GetSupplyBucket(supply) + " supply (" + supply + ") and " + craft.popularity + " popularity with peak "+craft.peak);

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
        bool verboseLogging = false;
        /*if (items.Count == 5 && items[0] == Item.BakedPumpkin && items[1] == Item.ParsnipSalad && items[2] == Item.OnionSoup && items[3] == Item.ParsnipSalad && items[4] == Item.OnionSoup)
            verboseLogging = true;*/

        int craftsAbove4 = GetNumCrafts() - 4;
        int daysToGroove = 5 - day;
        if (!Solver.Rested)
            daysToGroove--;

        if (verboseLogging)
            PluginLog.Debug("Calculating value for {0}, starting groove {3}, days to groove: {1}, crafts above 4: {2}", String.Join(", ", items), daysToGroove, craftsAbove4, startingGroove);
        int grooveValue = 0;

        if (daysToGroove > 0)
        {
            int fullGrooveBonus = (daysToGroove - 1) * Solver.GroovePerFullDay;
            grooveValue = fullGrooveBonus + Solver.GroovePerPartDay;
            grooveValue *= craftsAbove4;
        }
        if (verboseLogging)
            PluginLog.Debug("groove value: {0}", grooveValue);

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
            if (verboseLogging)
                PluginLog.Debug("Processing craft {0}, made previously: {1}, efficient: {2}", completedCraft.item, previouslyCrafted, efficient);

            workshopValue += GetValueForCurrent(day, previouslyCrafted, startingGroove + i * 3, efficient, verboseLogging);
            currentIndex++;
            int amountCrafted = efficient ? 6 : 3;
            numCrafted[completedCraft.item]= previouslyCrafted + amountCrafted;
        }
        int materialValue = (int)(GetMaterialCost() * Solver.MaterialWeight);
        if(verboseLogging)
        PluginLog.Debug("Groove value {2}, workshopValue {3}, material value {0}, weighted {1}", GetMaterialCost(), materialValue, grooveValue, workshopValue);
        //Allow for the accounting for materials if desired
        return grooveValue + workshopValue - materialValue;
    }

    public bool IsSafe(int day)
    {
        foreach(var item in crafts)
        {
            if (!item.PeaksOnOrBeforeDay(day))
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

    public bool UsesTooMany(Dictionary<Item, int>? limitedUse)
    {
        if (limitedUse == null)
            return false;

        Dictionary<Item, int> used = new Dictionary<Item, int>();


        for (int i = 0; i < items.Count; i++)
        {
            if (!used.ContainsKey(items[i]))
                used.Add(items[i], 3 + (i > 0 ? 3 : 0));
            else
                used[items[i]] = used[items[i]] + 3 + (i > 0 ? 3 : 0);
        }
        foreach (var kvp in used)
        {
            if (limitedUse.ContainsKey(kvp.Key) && limitedUse[kvp.Key] < used[kvp.Key])
            {
                return true;
            }
        }
        return false;
    }

    public Dictionary<Item, int> GetLimitedUses()
    {
        return GetLimitedUses(null);
    }

    public Dictionary<Item, int> GetLimitedUses(Dictionary<Item, int>? previousLimitedUses)
    {
        Dictionary<Item, int> limitedUses;
        if (previousLimitedUses == null)
            limitedUses = new Dictionary<Item, int>();
        else
            limitedUses = new Dictionary<Item, int>(previousLimitedUses);

        for (int i = 0; i < items.Count; i++)
        {
            if (!limitedUses.ContainsKey(items[i]))
                limitedUses.Add(items[i], 12);

            limitedUses[items[i]]= limitedUses[items[i]] - 3 - (i > 0 ? 3 : 0);
        }

        return limitedUses;
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
