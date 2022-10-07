
using System;
using System.Collections.Generic;
using Dalamud.Logging;

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
        new int[]{0, 0, -6, 0, 0, 0, 0}, //Unknown
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

    static PeakCycle[][] PEAKS_TO_CHECK = new PeakCycle[5][]
    {
        new PeakCycle[]{Cycle3Weak, Cycle3Strong, Cycle67, Cycle45}, //Day2
        new PeakCycle[] {Cycle4Weak, Cycle4Strong, Cycle6Weak, Cycle5, Cycle67}, //Day3
        new PeakCycle[] { Cycle5Weak, Cycle5Strong, Cycle6Strong, Cycle7Weak, Cycle7Strong}, //Day4
        new PeakCycle[] { Cycle6Weak, Cycle6Strong, Cycle7Weak, Cycle7Strong}, //Day5 (remedial)
        new PeakCycle[] { Cycle7Weak, Cycle7Strong} //Day6 (remedial)
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
    private Dictionary<int,ObservedSupply> observedSupplies;
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
        observedSupplies = new Dictionary<int, ObservedSupply>();

        if (mats != null)
        {
            foreach (var mat in mats)
            {
                materialValue += RareMaterialHelper.materialValues[mat.Key] * mat.Value;
            }
        }        
    }
    
    public bool GetsEfficiencyBonus(ItemInfo other)
    {
        if (other == null)
        {
            PluginLog.LogError("Trying to compare efficiency for null item?");
            return false;
        }
        bool diffItem = other.item != item;
        bool cat1Matches = category1 == other.category1 || category1 == other.category2;
        bool cat2Matches = category2 != Invalid && (category2 == other.category1 || category2 == other.category2);

        return diffItem && (cat1Matches || cat2Matches);
    }

    //Set start-of-week data
    public void SetInitialData(Popularity pop, PeakCycle prev)
    {
        popularity = pop;
        previousPeak = prev;
    }

    public void AddObservedDay(ObservedSupply ob, int day, int hour)
    {
        //PluginLog.LogDebug("Found observed supply {0} for item {1} on day {2} hour {3}", ob, item, day+1, hour);
        if (observedSupplies.ContainsKey(day))
            observedSupplies[day] = ob;
        else
            observedSupplies.Add(day, ob);

        if(day == Solver.CurrentDay)
            SetPeakBasedOnObserved(hour);
    }

    public void SetCrafted(int num, int day)
    {
        craftedPerDay[day] = num;
    }

    private int GetCraftedBeforeDay(int day)
    {
        int sum = 0;
        for (int c = 0; c < day; c++)
            sum += craftedPerDay[c];

        return sum;
    }

    private void SetPeakBasedOnObserved(int currentHour)
    {
        if (peak.IsTerminal() && peak != Cycle2Weak)
            return;

        CycleSchedule? currentDaySchedule = null;

        if (observedSupplies.ContainsKey(0) && observedSupplies[0].supply == Insufficient)
        {
            DemandShift observedDemand = observedSupplies[0].demandShift;

            if(observedDemand == None)
            {
                peak = Cycle2Strong;
                return;
            }
            else if(observedDemand == Increasing || observedDemand == Decreasing)
            {
                peak = Cycle2Weak;
                return;
            }
            else if (previousPeak.IsReliable())
            {
                if (observedDemand == Skyrocketing)
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
            else if (observedSupplies.ContainsKey(1))
            {
                int day = 1;
                if (Solver.Importer.endDays.Count > day)
                {
                    currentDaySchedule = new CycleSchedule(day, 0);
                    currentDaySchedule.SetForAllWorkshops(Solver.Importer.endDays[day].crafts);

                }
                PluginLog.LogDebug(item + " observed: " + observedSupplies[day]);
                int craftedToday = currentDaySchedule == null ? 0 : currentDaySchedule.GetCraftedBeforeHour(item, currentHour);
                if (craftedToday > 0)
                    PluginLog.Debug("Found {0} crafted before hour {1} today, including in expected supply", craftedToday, currentHour);

                int weakPrevious = GetSupplyOnDayByPeak(Cycle2Weak, day - 1);
                int weakSupply = GetSupplyOnDayByPeak(Cycle2Weak, day) + craftedToday;
                ObservedSupply expectedWeak = new ObservedSupply(GetSupplyBucket(weakSupply),
                        GetDemandShift(weakPrevious, weakSupply));
                PluginLog.LogDebug("Checking against peak Cycle2Weak, expecting: " + expectedWeak);
                if (observedSupplies[day].Equals(expectedWeak))
                {
                    peak = Cycle2Weak;
                    return;
                }

                int strongPrevious = GetSupplyOnDayByPeak(Cycle2Strong, day - 1);
                int strongSupply = GetSupplyOnDayByPeak(Cycle2Strong, day) + craftedToday;
                ObservedSupply expectedStrong = new ObservedSupply(GetSupplyBucket(strongSupply),
                        GetDemandShift(strongPrevious, strongSupply));

                PluginLog.LogDebug("Checking against peak Cycle2Strong, expecting: " + expectedStrong);
                if (observedSupplies[day].Equals(expectedStrong))
                {
                    peak = Cycle2Strong;
                    return;
                }
                else
                    PluginLog.LogWarning(item + " does not match any known demand shifts for day 2: " + observedSupplies[1] +" with "+craftedToday+" crafts.");
            }
            else
            {
                peak = Cycle2Weak;

                Solver.AddUnknownD2(item);
            }
        }
        else 
        {
            for (int day = 1; day < 6; day++)
            {
                if (!observedSupplies.ContainsKey(day))
                {
                    continue;
                }
                if (Solver.Importer.endDays.Count > day)
                {
                    currentDaySchedule = new CycleSchedule(day, 0);
                    currentDaySchedule.SetForAllWorkshops(Solver.Importer.endDays[day].crafts);
                }
                ObservedSupply observedToday = observedSupplies[day];
                PluginLog.LogDebug(item + " observed: " + observedToday+" on day "+(day+1));
                int craftedPreviously = GetCraftedBeforeDay(day);
                int craftedToday = currentDaySchedule == null? 0 : currentDaySchedule.GetCraftedBeforeHour(item, currentHour);
                if (craftedToday > 0)
                    PluginLog.Debug("Found {0} crafted before hour {1} today, including in expected supply along with the {2} crafted before today", craftedToday, currentHour, craftedPreviously);
                bool found = false;

                for (int i = 0; i < PEAKS_TO_CHECK[day - 1].Length; i++)
                {
                    PeakCycle potentialPeak = PEAKS_TO_CHECK[day - 1][i];
                    int expectedPrevious = GetSupplyOnDayByPeak(potentialPeak, day - 1);
                    int expectedSupply = GetSupplyOnDayByPeak(potentialPeak, day) + craftedToday;
                    ObservedSupply expectedObservation = new ObservedSupply(GetSupplyBucket(craftedPreviously + expectedSupply),
                            GetDemandShift(expectedPrevious, expectedSupply));
                        PluginLog.LogDebug("Checking against peak " + potentialPeak + ", expecting: " + expectedObservation);

                    if (observedToday.Equals(expectedObservation))
                    {
                            PluginLog.LogDebug("match found!");
                        peak = potentialPeak;
                        found = true;
                        if (peak.IsTerminal())
                            return;
                    }
                }

                if (!found)
                    PluginLog.LogWarning(item + " does not match any known patterns for day " + (day + 1) + " with observed "+ observedSupplies[day] + " and " + craftedToday + " crafts.");
            }
        }
    }

    public int GetSupplyOnDay(int day)
    {
        int supply = SUPPLY_PATH[(int)peak][0];
        for (int c = 1; c <= day; c++)
        {
            supply += craftedPerDay[c - 1];
            supply += SUPPLY_PATH[(int)peak][c];
        }

        return supply;
    }

    public bool Equals(ItemInfo other)
    {
        return item == other.item;
    }

    public bool PeaksOnOrBeforeDay(int day, bool borrow4Hours)
    {
        if (time == 4 && borrow4Hours)
            return true;
        if (peak == Cycle2Weak || peak == Cycle2Strong)
            return day > 0;
        if (peak == Cycle3Weak || peak == Cycle3Strong || peak == Unknown)
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

    public static Supply GetSupplyBucket(int supply)
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

    public static int GetSupplyOnDayByPeak(PeakCycle peak, int day)
    {
        int supply = SUPPLY_PATH[(int)peak][0];
        for (int c = 1; c <= day; c++)
            supply += SUPPLY_PATH[(int)peak][c];

        return supply;
    }

    public static DemandShift GetDemandShift(int prevSupply, int newSupply)
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
