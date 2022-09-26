using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IslandWorkshopSolver.Solver;

public class WorkshopSchedule
{
    private List<ItemInfo> crafts;
    private List<Item> items; //Just a dupe of crafts, but accessible
    private List<int> completionHours;
    public int currentIndex = 0; //Used for cycle scheduler to figure out crafts stuff

    public Dictionary<RareMaterial, int> rareMaterialsRequired { get; private set; }

    public WorkshopSchedule(List<Item> crafts)
    {
        completionHours = new List<int>();
        this.crafts = new List<ItemInfo>();
        items = new List<Item>();
        rareMaterialsRequired = new Dictionary<RareMaterial, int>();
        setCrafts(crafts);
    }

    public void setCrafts(List<Item> newCrafts)
    {
        crafts.Clear();
        newCrafts.ForEach(delegate (Item item) { crafts.Add(Solver.items[(int)item]); });
        int currentHour = 0;
        completionHours.Clear();
        items.Clear();
        foreach (ItemInfo craft in crafts)
        {
            currentHour += craft.time;
            items.Add(craft.item);

            completionHours.Add(currentHour);

            if (craft.materialsRequired != null)
                foreach (var indivMat in craft.materialsRequired)
                {
                    int previouslyNeeded = 0;
                    if(rareMaterialsRequired.ContainsKey(indivMat.Key))
                    {
                        previouslyNeeded = rareMaterialsRequired[indivMat.Key];
                    }
                    else
                    {
                        rareMaterialsRequired.Add(indivMat.Key, 0);
                    }
                    rareMaterialsRequired[indivMat.Key] = previouslyNeeded + indivMat.Value;
                }
                    
        }

    }

    public ItemInfo? getCurrentCraft()
    {
        if (currentIndex < crafts.Count)
            return crafts[currentIndex];
        return null;
    }

    public int getNumCrafts()
    {
        return crafts.Count;
    }

    public List<Item> getItems()
    {
        return items;
    }

    public bool currentCraftCompleted(int hour)
    {
        if (currentIndex >= crafts.Count)
            return false;

        if (completionHours[currentIndex] == hour)
            return true;
        return false;
    }

    public int getValueForCurrent(int day, int craftedSoFar, int currentGroove, bool isEfficient)
    {
        ItemInfo craft = crafts[currentIndex];
        int baseValue = craft.baseValue * Solver.WORKSHOP_BONUS * (100 + currentGroove) / 10000;
        int supply = craft.getSupplyOnDay(day) + craftedSoFar;
        int adjustedValue = baseValue * PopularityHelper.GetMultiplier(craft.popularity) * SupplyHelper.GetMultiplier(ItemInfo.getSupplyBucket(supply))/ 10000;

        if (isEfficient)
            adjustedValue *= 2;
        if (Solver.verboseCalculatorLogging)
            Dalamud.Chat.Print(craft.item + " is worth " + adjustedValue + " with " + currentGroove + " groove at " + ItemInfo.getSupplyBucket(supply) + " supply (" + supply + ") and " + craft.popularity + " popularity");

        return adjustedValue;
    }

    public bool currentCraftIsEfficient()
    {
        if (currentIndex > 0 && currentIndex < crafts.Count)
            if (crafts[currentIndex].getsEfficiencyBonus(crafts[currentIndex - 1]))
                return true;

        return false;
    }

    public int getMaterialCost()
    {
        int cost = 0;
        foreach (ItemInfo craft in crafts)
        {
            cost += craft.materialValue;
        }
        return cost;
    }

    public int getValueWithGrooveEstimate(int day, int startingGroove)
    {
        int craftsAbove4 = 0;
        craftsAbove4 += getNumCrafts() - 4;
        int daysToGroove = 4 - day;
        if (!Solver.rested)
            daysToGroove--;

        if (daysToGroove < 0)
            daysToGroove = 0;

        int workshopValue = 0;
        Dictionary<Item, int> numCrafted = new Dictionary<Item, int>();
        currentIndex = 0;
        for (int i = 0; i < getNumCrafts(); i++)
        {
            ItemInfo? completedCraft = getCurrentCraft();
            if (completedCraft == null)
                continue;
            bool efficient = currentCraftIsEfficient();


            int previouslyCrafted = 0;
            if (numCrafted.ContainsKey(completedCraft.item))
            {
                previouslyCrafted = numCrafted[completedCraft.item];
            }
            else
            {
                numCrafted.Add(completedCraft.item, 0);
            }

            workshopValue += getValueForCurrent(day, previouslyCrafted, startingGroove + i * 3, efficient);
            currentIndex++;
            int amountCrafted = efficient ? 6 : 3;
            numCrafted[completedCraft.item]= previouslyCrafted + amountCrafted;
        }

        //Allow for the accounting for materials if desired
        return craftsAbove4 * daysToGroove * Solver.groovePerDay + workshopValue - (int)(getMaterialCost() * Solver.materialWeight);
    }

    public bool isSafe(int day)
    {
        foreach(var item in crafts)
        {
            if (!item.peaksOnOrBeforeDay(day, false))
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as WorkshopSchedule);
    }

    public bool Equals(WorkshopSchedule? other)
    {
        if (other == null)
            return false;
        return rareMaterialsRequired.Equals(other.rareMaterialsRequired);
    }

    public override int GetHashCode()
    {
        return rareMaterialsRequired.GetHashCode();
    }
}
