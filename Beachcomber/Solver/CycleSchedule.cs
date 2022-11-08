
using System.Collections.Generic;
using Dalamud.Logging;

namespace Beachcomber.Solver;

public class CycleSchedule
{
    private int day;
    public int startingGroove { get; set; }
    public int endingGroove { get; private set; }
    public WorkshopSchedule[] workshops = new WorkshopSchedule[3];
    public Dictionary<Item, int> numCrafted { get; private set; }

    public List<int> cowriesPerHour { get; private set; }

    public CycleSchedule(int day, int groove)
    {
        this.day = day;
        workshops = new WorkshopSchedule[Solver.NumWorkshops];
        
        startingGroove = groove;
        numCrafted = new Dictionary<Item, int>();
        cowriesPerHour = new List<int>();
    }

    public void SetForAllWorkshops(List<Item> crafts)
    {
        for (int i = 0; i < workshops.Length; i++)
            workshops[i] = new WorkshopSchedule(crafts);
    }

    public void SetWorkshop(int index, List<Item> crafts)
    {
        if (workshops[index] == null)
            workshops[index] = new WorkshopSchedule(crafts);
        else
            workshops[index].SetCrafts(crafts);
    }

    public int GetValue()
    {
        cowriesPerHour.Clear();
        numCrafted.Clear();

        for (int i = 0; i < workshops.Length; i++)
            workshops[i].currentIndex = 0;

        int currentGroove = startingGroove;

        int totalCowries = 0;
        for (int hour = 4; hour <= 24; hour += 2) //Nothing can finish until hour 4
        {
            Dictionary<Item, int> craftedThisHour = new Dictionary<Item, int>();
            int grooveToAdd = 0;
            int cowriesThisHour = 0;
            for (int i = 0; i < workshops.Length; i++)
            {
                if (workshops[i].CurrentCraftCompleted(hour))
                {
                    ItemInfo? completedCraft = workshops[i].GetCurrentCraft();
                    if (completedCraft == null)
                        continue;
                    bool efficient = workshops[i].CurrentCraftIsEfficient();


                    //PluginLog.LogVerbose("Found completed " + completedCraft.item + " at hour " + hour + ". Efficient? " + efficient);
                    int craftedEarlierThisHour = 0;
                    if (craftedThisHour.ContainsKey(completedCraft.item))
                    {
                        craftedEarlierThisHour = craftedThisHour[completedCraft.item];
                        //PluginLog.LogVerbose("Found completed " + completedCraft.item + " earlier this hour " + craftedEarlierThisHour);
                    }
                    else
                    {
                        craftedThisHour.Add(completedCraft.item, 0);

                        //PluginLog.LogVerbose("Did not find completed " + completedCraft.item + " earlier " + craftedEarlierThisHour+", adding");
                    }
                    craftedThisHour[completedCraft.item] = craftedEarlierThisHour + (efficient ? 2 : 1);

                    //PluginLog.LogVerbose("total crafted " + completedCraft.item + " at hour "+hour+": " + craftedThisHour[completedCraft.item]);
                    int numCraftedPreviously = numCrafted.TryGetValue(completedCraft.item, out int craftedPreviously) ? craftedPreviously : 0;

                    //PluginLog.LogVerbose("total crafted " + completedCraft.item + " before hour " + hour + ": " + numCraftedPreviously);
                    cowriesThisHour += workshops[i].GetValueForCurrent(day, numCraftedPreviously, currentGroove, efficient);

                    //PluginLog.LogVerbose("cowries at hour " + hour + ": " + cowriesThisHour);
                    workshops[i].currentIndex++;
                    if (workshops[i].CurrentCraftIsEfficient())
                        grooveToAdd++;
                }
            }
            if (cowriesThisHour > 0)
            {
                cowriesPerHour.Add(cowriesThisHour);
                //PluginLog.LogVerbose("hour " + hour + ": " + cowriesThisHour);
            }

            totalCowries += cowriesThisHour;
            currentGroove += grooveToAdd;
            if (currentGroove > Solver.GROOVE_MAX)
                currentGroove = Solver.GROOVE_MAX;

            foreach(var craft in craftedThisHour)
            {
                if(numCrafted.ContainsKey(craft.Key))
                    numCrafted[craft.Key] = numCrafted[craft.Key] + craft.Value;
                else
                    numCrafted.Add(craft.Key, craft.Value);
            }  
        }

        endingGroove = currentGroove;
       
       return totalCowries;
       
    }

    public int GetCraftedBeforeHour(Item item, int currentHour)
    {
        for (int i = 0; i < workshops.Length; i++)
            workshops[i].currentIndex = 0;

        int totalCrafted = 0;
        for (int hour = 4; hour <= currentHour; hour += 2) //Nothing can finish until hour 4
        {
            for (int i = 0; i < workshops.Length; i++)
            {
                if (workshops[i].CurrentCraftCompleted(hour))
                {
                    ItemInfo? completedCraft = workshops[i].GetCurrentCraft();
                    if (completedCraft == null)
                        continue;
                    if(completedCraft.item == item)
                        totalCrafted += (workshops[i].CurrentCraftIsEfficient() ? 2 : 1);

                   
                    workshops[i].currentIndex++;
                }
            }
        }
        return totalCrafted;
    }

    public bool HasAnyUnsurePeaks()
    {
        return workshops[0].HasAnyUnsurePeaks();
    }

    public int GetMaterialCost()
    {
        int cost = 0;
        foreach(WorkshopSchedule shop in workshops)
        {
            cost += shop.GetMaterialCost();
        }
        return cost;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CycleSchedule);
    }

    public bool Equals(CycleSchedule? other)
    {
        if (other == null)
            return false;
        return workshops.Equals(other.workshops);
    }

    public override int GetHashCode()
    {
        return workshops.GetHashCode();
    }    
}
