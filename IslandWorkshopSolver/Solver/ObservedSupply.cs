using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IslandWorkshopSolver.Solver;

public class ObservedSupply
{
    public Supply supply;
    public DemandShift demandShift;

    public ObservedSupply(Supply supp, DemandShift demand)
    {
        supply = supp;
        demandShift = demand;
    }

    public bool Equals(ObservedSupply other)
    {
        return other.supply == this.supply && other.demandShift == this.demandShift;
    }

    public override string ToString()
    {
        return supply + " - " + demandShift;
    }
}
