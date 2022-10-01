using System.Collections.Generic;
namespace IslandWorkshopSolver.Solver;

public class EndDaySummary
{
    private List<int> numCrafted;
    public int endingGroove { get; private set; }
    public int endingGross { get; set; }
    public int endingNet { get; set; }  

    public List<Item> crafts { get; private set; }
    public List<int> valuesPerCraft { get; set; }
    public EndDaySummary(List<int> numCrafted, int endingGroove, int gross, int net, List<Item> crafts)
    {
        this.numCrafted = numCrafted;
        this.endingGroove = endingGroove;
        this.endingGross = gross;
        this.endingNet = net;
        this.crafts = crafts;
        valuesPerCraft = new List<int>();
    }

    public int getCrafted(int itemIndex)
    {
        return numCrafted[itemIndex];
    }

    public int totalCraftedItems()
    {
        int sum = 0;
        foreach(var item in numCrafted)
        {
            sum += item;
        }
        return sum;
    }

    public int craftedItems()
    {
        return numCrafted.Count;
    }
    public override string ToString()
    {
        return "groove: "+endingGroove+" gross: "+endingGross+" net: " + endingNet;
    }
}
