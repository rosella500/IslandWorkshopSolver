using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IslandWorkshopSolver.Solver;

public class EndDaySummary
{
    private List<int> numCrafted;
    public int endingGroove { get; private set; }
    public int endingGross { get; private set; }
    public int endingNet { get; private set; }  

    public EndDaySummary(int groove, int gross, int net, List<int> crafted)
    {
        
        numCrafted = crafted;

        endingGroove = groove;
        endingGross = gross;
        endingNet = net;

        Dalamud.Chat.Print("Making new end-day summary: " + ToString());
    }
    public int getCrafted(int itemIndex)
    {
        return numCrafted[itemIndex];
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
