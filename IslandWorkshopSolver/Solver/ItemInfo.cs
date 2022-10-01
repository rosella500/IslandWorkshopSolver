using FFXIVClientStructs;
using IslandWorkshopSolver.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IslandWorkshopSolver.Solver.PeakCycle;
using static IslandWorkshopSolver.Solver.ItemCategory;
using static IslandWorkshopSolver.Solver.DemandShift;
using static IslandWorkshopSolver.Solver.Supply;


namespace IslandWorkshopSolver.Solver;

public class ItemInfo
{
    //Contains exact supply values for concrete paths and worst-case supply values for tentative ones
    static int[][] SUPPLY_PATH = new int[16][]
    { 
        new int[]{0, 0, 0, 0, 0, 0, 0}, //Unknown
            new int[]{-4, -4, 10, 0, 0, 0, 0}, //Cycle2Weak 
            new int[]{-8, -7, 15, 0, 0, 0, 0}, //Cycle2Strong
            new int[]{0, -4, -4, 10, 0, 0, 0}, //Cycle3Weak
            new int[]{0, -8, -7, 15, 0, 0, 0}, //Cycle3Strong
            new int[]{0, 0, -4, -4, 10, 0, 0}, //Cycle4Weak
            new int[]{0, 0, -8, -7, 15, 0, 0}, //Cycle4Strong
            new int[]{0, 0, 0, -4, -4, 10, 0}, //5Weak
            new int[]{0, 0, 0, -8, -7, 15, 0}, //5Strong
            new int[]{0, -1, 5, -4, -4, -4, 10}, //6Weak
            new int[]{0, -1, 8, -7, -8, -7, 15}, //6Strong
            new int[]{0, -1, 8, -3, -4, -4, -4}, //7Weak
            new int[]{0, -1, 8, 0, -7, -8, -7}, //7Strong
            new int[]{0, 0, 0, -6, 0, 10, 0}, //4/5
            new int[]{0, 0, 0, -4, -4, 10, 0}, //5
            new int[]{0, -1, 8, 0, -7, -6, 0} //6/7
            };

    static PeakCycle[][] PEAKS_TO_CHECK = new PeakCycle[3][]
    {
        new PeakCycle[]{Cycle3Weak, Cycle3Strong, Cycle67, Cycle45}, //Day2
        new PeakCycle[] {Cycle4Weak, Cycle4Strong, Cycle6Weak, Cycle5, Cycle67}, //Day3
        new PeakCycle[] { Cycle5Weak, Cycle5Strong, Cycle6Strong, Cycle7Weak, Cycle7Strong} //Day4
    };

//Constant info
    public Item item { get; private set; }
    public int baseValue { get; private set; }
    public ItemCategory category1 { get; private set; }
    public ItemCategory category2 { get; private set; }
    public int time { get; private set; }
    public Dictionary<RareMaterial, int>? materialsRequired { get; private set; }
    public int materialValue { get; private set; }

    //Weekly info
    public Popularity popularity { get; private set; }
    public PeakCycle previousPeak { get; private set; }
    public PeakCycle peak { get; set; } //This should be a private set but I'm allowing it so I can test different peaks
    public int[] craftedPerDay { get; private set; }
    private List<ObservedSupply> observedSupplies;
    public int rankUnlocked { get; private set; }

    public ItemInfo(Item i, ItemCategory cat1, ItemCategory cat2, int value, int hours, int rank, Dictionary<RareMaterial, int>? mats)
    {
        item = i;
        baseValue = value;
        category1 = cat1;
        category2 = cat2;
        time = hours;
        materialsRequired = mats;
        materialValue = 0;
        rankUnlocked = rank;
        craftedPerDay = new int[7];
        observedSupplies = new List<ObservedSupply>();

        if (mats != null)
        {
            foreach (var mat in mats)
            {
                materialValue += RareMaterialHelper.materialValues[mat.Key] * mat.Value;
            }
        }        
    }
    
    public bool getsEfficiencyBonus(ItemInfo other)
    {
        if (other == null)
        {
            Dalamud.Chat.PrintError("Trying to compare efficiency for null item?");
            return false;
        }
        bool diffItem = other.item != item;
        bool cat1Matches = category1 == other.category1 || category1 == other.category2;
        bool cat2Matches = category2 != Invalid && (category2 == other.category1 || category2 == other.category2);

        return diffItem && (cat1Matches || cat2Matches);
    }

    //Set start-of-week data
    public void setInitialData(Popularity pop, PeakCycle prev, ObservedSupply ob)
    {
        popularity = pop;
        previousPeak = prev;
        addObservedDay(ob, 0, 0);
    }

    public void addObservedDay(ObservedSupply ob, int day, int hour)
    {
        if (observedSupplies.Count > day)
            observedSupplies[day] = ob;
        else
            observedSupplies.Add(ob);
        setPeakBasedOnObserved(hour);
    }

    public void setCrafted(int num, int day)
    {
        craftedPerDay[day] = num;
    }

    private int getCraftedBeforeDay(int day)
    {
        int sum = 0;
        for (int c = 0; c < day; c++)
            sum += craftedPerDay[c];

        return sum;
    }

    private void setPeakBasedOnObserved(int currentHour)
    {
        if (peak.IsTerminal() && peak != Cycle2Weak)
            return;

        CycleSchedule? currentDaySchedule = null;

        if (observedSupplies[0].supply == Insufficient)
        {
            DemandShift observedDemand = observedSupplies[0].demandShift;

            if (previousPeak.IsReliable())
            {
                if (observedDemand == None || observedDemand == Skyrocketing)
                {
                    peak = Cycle2Strong;
                    return;
                }
                else
                {
                    peak = Cycle2Weak;
                    return;
                }
            }
            else if (observedSupplies.Count > 1)
            {
                int day = 1;
                if (Solver.importer.endDays.Count > day)
                {
                    currentDaySchedule = new CycleSchedule(day, 0);
                    currentDaySchedule.setForAllWorkshops(Solver.importer.endDays[day].crafts);

                }

                int craftedToday = currentDaySchedule == null ? 0 : currentDaySchedule.getCraftedBeforeHour(item, currentHour);
                int weakPrevious = getSupplyOnDayByPeak(Cycle2Weak, day - 1);
                int weakSupply = getSupplyOnDayByPeak(Cycle2Weak, day) + craftedToday;
                ObservedSupply expectedWeak = new ObservedSupply(getSupplyBucket(weakSupply),
                        getDemandShift(weakPrevious, weakSupply));

                if (observedSupplies[day].Equals(expectedWeak))
                {
                    peak = Cycle2Weak;
                    return;
                }

                int strongPrevious = getSupplyOnDayByPeak(Cycle2Strong, day - 1);
                int strongSupply = getSupplyOnDayByPeak(Cycle2Strong, day) + craftedToday;
                ObservedSupply expectedStrong = new ObservedSupply(getSupplyBucket(strongSupply),
                        getDemandShift(strongPrevious, strongSupply));

                if (observedSupplies[day].Equals(expectedStrong))
                {
                    peak = Cycle2Strong;
                    return;
                }
                else
                    Dalamud.Chat.Print(item + " does not match any known demand shifts for day 2: " + observedSupplies[1]);
            }
            else
            {
                peak = Cycle2Weak;

                Solver.addUnknownD2(item);
                /*if (previousPeak == Cycle7Strong)
                    Dalamud.Chat.Print("Warning! Can't tell if " + item + " is a weak or a strong 2 peak.");
                    else
                    Dalamud.Chat.Print("Need to craft " + item + " to determine weak or strong 2 peak, assuming weak.");*/
            }
        }
        else if (observedSupplies.Count > 1)
        {
            int daysToCheck = Math.Min(4, observedSupplies.Count);
            for (int day = 1; day < daysToCheck; day++)
            {
                if (Solver.importer.endDays.Count > day)
                {
                    currentDaySchedule = new CycleSchedule(day, 0);
                    currentDaySchedule.setForAllWorkshops(Solver.importer.endDays[day].crafts);
                }

                ObservedSupply observedToday = observedSupplies[day];
                if (Solver.verboseCalculatorLogging)
                    Dalamud.Chat.Print(item + " observed: " + observedToday);
                int craftedPreviously = getCraftedBeforeDay(day);
                int craftedToday = currentDaySchedule == null? 0 : currentDaySchedule.getCraftedBeforeHour(item, currentHour);
                bool found = false;

                for (int i = 0; i < PEAKS_TO_CHECK[day - 1].Length; i++)
                {
                    PeakCycle potentialPeak = PEAKS_TO_CHECK[day - 1][i];
                    int expectedPrevious = getSupplyOnDayByPeak(potentialPeak, day - 1);
                    int expectedSupply = getSupplyOnDayByPeak(potentialPeak, day) + craftedToday;
                    ObservedSupply expectedObservation = new ObservedSupply(getSupplyBucket(craftedPreviously + expectedSupply),
                            getDemandShift(expectedPrevious, expectedSupply));
                    if (Solver.verboseCalculatorLogging)
                        Dalamud.Chat.Print("Checking against peak " + potentialPeak + ", expecting: " + expectedObservation);

                    if (observedToday.Equals(expectedObservation))
                    {
                        if (Solver.verboseCalculatorLogging)
                            Dalamud.Chat.Print("match found!");
                        peak = potentialPeak;
                        found = true;
                        if (peak.IsTerminal())
                            return;
                    }
                }

                if (!found)
                    Dalamud.Chat.Print(item + " does not match any known patterns for day " + (day + 1));
            }
        }
    }

    public int getValueWithSupply(Supply supply)
    {
        int workshopBase = baseValue * Solver.WORKSHOP_BONUS / 100;
        return workshopBase * SupplyHelper.GetMultiplier(supply) * PopularityHelper.GetMultiplier(popularity) / 10000;
    }

    public int getSupplyAfterCraft(int day, int newCrafts)
    {
        return getSupplyOnDay(day) + newCrafts;
    }

    public int getSupplyOnDay(int day)
    {
        int supply = SUPPLY_PATH[(int)peak][0];
        for (int c = 1; c <= day; c++)
        {
            supply += craftedPerDay[c - 1];
            supply += SUPPLY_PATH[(int)peak][c];
        }

        return supply;
    }

    public Supply getSupplyBucketOnDay(int day)
    {
        return getSupplyBucket(getSupplyOnDay(day));
    }
    public Supply getSupplyBucketAfterCraft(int day, int newCrafts)
    {
        return getSupplyBucket(getSupplyAfterCraft(day, newCrafts));
    }

    public bool Equals(ItemInfo other)
    {
        return item == other.item;
    }

    public bool peaksOnOrBeforeDay(int day, bool borrow4Hours)
    {
        if (time == 4 && borrow4Hours)
            return true;
        if (peak == Cycle2Weak || peak == Cycle2Strong)
            return day > 0;
        if (peak == Cycle3Weak || peak == Cycle3Strong)
            return day > 1;
        if (peak == Cycle4Weak || peak == Cycle4Strong || peak == Cycle45)
            return day > 2;
        if (peak == Cycle5Weak || peak == Cycle5Strong || peak == Cycle5)
            return day > 3;
        if (peak == Cycle6Weak || peak == Cycle6Strong || peak == Cycle67)
            return day > 4;
        if (peak == Cycle7Weak || peak == Cycle7Strong)
            return day > 5;

        return false; //Peak is Unknown, so definitely hasn't passed
    }

    public static Supply getSupplyBucket(int supply)
    {
        if (supply < -8)
            return Nonexistent;
        if (supply < 0)
            return Insufficient;
        if (supply < 8)
            return Sufficient;
        if (supply < 16)
            return Surplus;
        return Overflowing;
    }

    public static int getSupplyOnDayByPeak(PeakCycle peak, int day)
    {
        int supply = SUPPLY_PATH[(int)peak][0];
        for (int c = 1; c <= day; c++)
            supply += SUPPLY_PATH[(int)peak][c];

        return supply;
    }

    public static DemandShift getDemandShift(int prevSupply, int newSupply)
    {
        int diff = newSupply - prevSupply;
        if (diff < -5)
            return Skyrocketing;
        if (diff < -1)
            return Increasing;
        if (diff < 2)
            return None;
        if (diff < 6)
            return Decreasing;
        return Plummeting;
    }

    public override string ToString()
    {
        return item + ", " + peak;
    }
}
