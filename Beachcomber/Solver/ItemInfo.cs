
using System;
using System.Collections.Generic;
using Dalamud.Logging;

using static Beachcomber.Solver.PeakCycle;
using static Beachcomber.Solver.ItemCategory;
using static Beachcomber.Solver.DemandShift;
using static Beachcomber.Solver.Supply;
using System.Net.Sockets;


namespace Beachcomber.Solver;

public class ItemInfo
{
    //Contains exact supply values for concrete paths and worst-case supply values for tentative ones
    static int[][] SUPPLY_PATH = new int[18][]
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
            new int[]{0, 0, 0, -8, 0, 10, 0}, //4/5
            new int[]{0, 0, 0, -4, -4, 10, 0}, //5
            new int[]{0, -1, 8, 0, -7, -8, 0}, //6/7
            new int[]{0, 0, -8, 0, 0, 0, 0}, //UnknownD1
            new int[]{-4, -4, 10, 0, 0, 0, 0 } //Cycle2Unknown
            };

    static PeakCycle[][] PEAKS_TO_CHECK = new PeakCycle[5][]
    {
        new PeakCycle[] { Cycle3Strong, Cycle67, Cycle45, Cycle2Strong, Cycle2Weak }, //Day2
        new PeakCycle[] { Cycle4Weak, Cycle4Strong, Cycle6Weak, Cycle5, Cycle67 }, //Day3
        new PeakCycle[] { Cycle5Weak, Cycle5Strong, Cycle6Strong, Cycle7Weak, Cycle7Strong, Cycle4Weak, Cycle4Strong }, //Day4
        new PeakCycle[] { Cycle6Weak, Cycle6Strong, Cycle7Weak, Cycle7Strong, Cycle5Weak, Cycle5Strong }, //Day5 (remedial)
        new PeakCycle[] { Cycle7Weak, Cycle7Strong, Cycle6Weak, Cycle6Strong } //Day6 (remedial)
    };

//Constant info
    public Item item { get; private set; }
    public int baseValue { get; private set; }
    public ItemCategory category1 { get; private set; }
    public ItemCategory category2 { get; private set; }
    public int time { get; private set; }
    public Dictionary<Material, int> materialsRequired { get; private set; }
    public int materialValue { get; private set; }

    //Weekly info
    public Popularity popularity { get; private set; }
    public PeakCycle previousPeak { get; private set; }

    private PeakCycle _peak = Unknown;
    public PeakCycle peak { get { return _peak; } set { _peak = value; PluginLog.Verbose("Setting item {0} to peak {1}", item, peak); } } //This should be a private set but I'm allowing it so I can test different peaks
    public int[] craftedPerDay { get; private set; }
    private Dictionary<int,ObservedSupply> observedSupplies;
    public int rankUnlocked { get; private set; }

    public ItemInfo(Item i, ItemCategory cat1, ItemCategory cat2, int value, int hours, int rank, Dictionary<Material, int> mats)
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
                if(RareMaterialHelper.GetMaterialValue(mat.Key, out int sellValue))
                    materialValue += sellValue * mat.Value;
            }
        }        
    }
    public void ResetForWeek()
    {
        craftedPerDay = new int[7];
        observedSupplies = new Dictionary<int, ObservedSupply>();
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
            SetPeakBasedOnObserved(day, hour);
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

    public void SetPeakBasedOnObserved(int day, int currentHour)
    {
        if (peak.IsTerminal())
            return;

        CycleSchedule? currentDaySchedule = null;
        
        if(day == 0 && observedSupplies.ContainsKey(0))
        {
            if (observedSupplies[0].supply == Insufficient)
            {
                DemandShift observedDemand = observedSupplies[0].demandShift;

                if (observedDemand == None)
                {
                    peak = Cycle2Strong;
                    PluginLog.LogDebug("{0} has reliable D2 peak {1}", item, peak);
                    return;
                }
                else if (observedDemand == Increasing)
                {
                    peak = Cycle2Weak;
                    PluginLog.LogDebug("{0} has reliable D2 peak {1}", item, peak);
                    return;
                }
                else if (previousPeak.IsReliable())
                {
                    if (observedDemand == Skyrocketing)
                    {
                        peak = Cycle2Strong;
                        PluginLog.LogDebug("{0} has reliable D2 peak {1}", item, peak);
                        return;
                    }
                    else
                    {
                        peak = Cycle2Weak;
                        PluginLog.LogDebug("{0} has reliable D2 peak {1}", item, peak);
                        return;
                    }
                }
                else
                {
                    peak = Cycle2Unknown;
                    PluginLog.LogDebug("{0} peaks D2 but strength is unknown", item);
                    Solver.AddUnknownD2(item);
                }
            }
            else
                peak = UnknownD1;

            return;
        }
        else if(day == 1 && observedSupplies.ContainsKey(1) && peak == Cycle2Unknown)
        {
            if (Solver.Importer.endDays.Count > day)
            {
                currentDaySchedule = new CycleSchedule(day, 0);
                currentDaySchedule.SetForAllWorkshops(Solver.Importer.endDays[day].crafts);
            }

            PluginLog.LogVerbose(item + " observed: " + observedSupplies[day]);
            int craftedToday = currentDaySchedule == null ? 0 : currentDaySchedule.GetCraftedBeforeHour(item, currentHour);
            if (craftedToday > 0)
                PluginLog.Debug("Found {0} crafted before hour {1} today, including in expected supply", craftedToday, currentHour);

            int weakPrevious = GetSupplyOnDayByPeak(Cycle2Weak, day - 1);
            int weakSupply = GetSupplyOnDayByPeak(Cycle2Weak, day) + craftedToday;
            ObservedSupply expectedWeak = new ObservedSupply(GetSupplyBucket(weakSupply),
                    GetDemandShift(weakPrevious, weakSupply));
            PluginLog.LogVerbose("Checking against peak Cycle2Weak, expecting: " + expectedWeak);
            if (observedSupplies[day].Equals(expectedWeak))
            {
                peak = Cycle2Weak;
                PluginLog.LogDebug("{0} has observed D2 peak {1}", item, peak);
                return;
            }

            int strongPrevious = GetSupplyOnDayByPeak(Cycle2Strong, day - 1);
            int strongSupply = GetSupplyOnDayByPeak(Cycle2Strong, day) + craftedToday;
            ObservedSupply expectedStrong = new ObservedSupply(GetSupplyBucket(strongSupply),
                    GetDemandShift(strongPrevious, strongSupply));

            PluginLog.LogVerbose("Checking against peak Cycle2Strong, expecting: " + expectedStrong);
            if (observedSupplies[day].Equals(expectedStrong))
            {
                peak = Cycle2Strong;
                PluginLog.LogDebug("{0} has observed D2 peak {1}", item, peak);
                return;
            }
            else
                PluginLog.LogWarning(item + " does not match any known demand shifts for day 2: " + observedSupplies[1] + " with " + craftedToday + " crafts.");
        }
        
        if(day == 2 && observedSupplies.ContainsKey(2) && peak == Cycle67)
        {
            if (Solver.Importer.endDays.Count > day)
            {
                currentDaySchedule = new CycleSchedule(day, 0);
                currentDaySchedule.SetForAllWorkshops(Solver.Importer.endDays[day].crafts);
            }

            PluginLog.LogVerbose(item + " observed: " + observedSupplies[day]);
            int craftedToday = currentDaySchedule == null ? 0 : currentDaySchedule.GetCraftedBeforeHour(item, currentHour);
            if (craftedToday > 0)
                PluginLog.Debug("Found {0} crafted before hour {1} today, including in expected supply", craftedToday, currentHour);

            int weakPrevious = GetSupplyOnDayByPeak(Cycle3Weak, day - 1);
            int weakSupply = GetSupplyOnDayByPeak(Cycle3Weak, day) + craftedToday;
            ObservedSupply expectedWeak = new ObservedSupply(GetSupplyBucket(weakSupply),
                    GetDemandShift(weakPrevious, weakSupply));
            PluginLog.LogVerbose("Checking against peak Cycle3Weak, expecting: " + expectedWeak);
            if (observedSupplies[day].Equals(expectedWeak))
            {
                peak = Cycle3Weak;
                PluginLog.LogDebug("{0} has observed D2 peak {1}", item, peak);
                return;
            }
        }
        
        if(observedSupplies.ContainsKey(day))
        {
            if (Solver.Importer.endDays.Count > day)
            {
                currentDaySchedule = new CycleSchedule(day, 0);
                currentDaySchedule.SetForAllWorkshops(Solver.Importer.endDays[day].crafts);
            }
            ObservedSupply observedToday = observedSupplies[day];
            PluginLog.LogVerbose(item + " observed: " + observedToday+" on day "+(day+1));
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
                PluginLog.LogVerbose("Checking against peak " + potentialPeak + ", expecting: " + expectedObservation);

                if (observedToday.Equals(expectedObservation))
                {
                    peak = potentialPeak;
                    PluginLog.Debug("{0} with {3} crafts matches pattern for peak {1}, terminal? {2}", item, peak, peak.IsTerminal(), craftedPreviously+craftedToday);
                    found = true;
                    if (peak.IsTerminal())
                        return;
                }
            }

            if (!found)
                PluginLog.LogWarning("{0} does not match any known patterns for day {1} with observed {2}, {3} crafted before and {4} crafted today", item,day+1,observedSupplies[day], craftedPreviously,craftedToday);
            
        }
        PluginLog.Warning("Item {0} contains no observed data for day {1}. Can't set peak.", item, day);
    }

    public int GetSupplyOnDay(int day)
    {
        /*observedSupplies.TryGetValue(day, out var observedToday);
        observedSupplies.TryGetValue(day - 1, out var observedEarlier);    */    
        
        int supply = SUPPLY_PATH[(int)peak][0];
        for (int c = 1; c <= day; c++)
        {
            supply += craftedPerDay[c - 1];
            supply += SUPPLY_PATH[(int)peak][c];
        }

        /*if(observedEarlier!= null && Solver.Importer.endDays[day - 1] != null)
        {
            int craftedToday = 0;
            if(Solver.Importer.endDays[day - 1].crafts.Count > 0)
            {
                CycleSchedule schedule = new CycleSchedule(day - 1, 0); //groove doesn't matter for this
                schedule.SetForAllWorkshops(Solver.Importer.endDays[day - 1].crafts);
                craftedToday = schedule.GetCraftedBeforeHour(item, Solver.Importer.observedSupplyHours[day - 1]);
                *//*if (craftedToday > 0)
                    PluginLog.Debug("Found {0}x {3} crafted already today, so we're looking at a supply of {1} when observed as {2}", craftedToday, craftedToday + supply, observedEarlier, item);*//*
            }
            
            //Only return exact supply number if it matches what we observed
            if (GetSupplyBucket(supply + craftedToday) == observedEarlier.supply)
            {
                return supply + SUPPLY_PATH[(int)peak][day] + craftedPerDay[day - 1];
            }
            else
            {
                PluginLog.Warning("Estimated supply for item {4} day {3}: {0} ({1}) doesn't match observed supply: {2}",
                    supply, GetSupplyBucket(supply), observedEarlier.supply, day, item);

                if (observedToday != null)
                {
                    //Make best guess using last observed day
                    Supply observedSupply = observedToday.supply;
                    return GetEstimatedSupplyNum(observedSupply);
                }
                else
                {
                    Supply yesterdaySupply = observedEarlier.supply;
                    if (peak == Unknown)
                    {

                        int estimatedSupply = GetEstimatedSupplyTomorrow(yesterdaySupply);
                        //PluginLog.Debug("Assuming it peaked in the past, sending supply {0} ({1})", estimatedSupply, GetSupplyBucket(estimatedSupply));
                        return estimatedSupply;
                    }
                    else
                    {
                        int estimatedYesterday = GetEstimatedSupplyNum(yesterdaySupply);

                        if (peak.IsTerminal())
                        {
                            if ((int)peak == day * 2) //Strong peaking on the day we're estimating for
                                estimatedYesterday -= 4;
                            else if ((int)peak % 2 == 0) //Strong peaked in the past
                                estimatedYesterday -= 2;
                        }

                        int finalEstimate = estimatedYesterday + SUPPLY_PATH[(int)peak][day];
                        PluginLog.Debug("We know its peak, so estimating today as {0} and adding {1} for tomorrow, sending supply {2} ({3})",
                            estimatedYesterday, SUPPLY_PATH[(int)peak][day], finalEstimate, GetSupplyBucket(finalEstimate));
                        return finalEstimate;
                    }
                }
            }
        }*/


        return supply;
    }

    public int GetValueWithSupply(Supply supply)
    {
        int value = baseValue * Solver.WORKSHOP_BONUS / 100;
        return value * SupplyHelper.GetMultiplier(supply) * PopularityHelper.GetMultiplier(popularity) / 10000;
    }

    public bool Equals(ItemInfo other)
    {
        return item == other.item;
    }

    public bool PeaksOnOrBeforeDay(int day)
    {
        if (time == 4)
            return true;
        if (Solver.ReservedItems.Count > 0 && !Solver.ReservedItems.Contains(item))
            return true;

        if (peak == Cycle2Weak || peak == Cycle2Strong)
            return day > 0;
        if (peak == Cycle3Weak || peak == Cycle3Strong || peak == UnknownD1)
            return day > 1;
        if (peak == Cycle4Weak || peak == Cycle4Strong || peak == Cycle45)
            return day > 2;
        if (peak == Cycle5Weak || peak == Cycle5Strong || peak == Cycle5)
            return day > 3;
        if (peak == Cycle6Weak || peak == Cycle6Strong || peak == Cycle67)
            return day > 4;
        if (peak == Cycle7Weak || peak == Cycle7Strong || peak == Unknown)
            return day > 5;

        return false; //Peak is Unknown, so definitely hasn't passed
    }

    public bool couldPrePeak(int day)
    {
        if (peak == Cycle45)
            return day == 2;
        else if (peak == UnknownD1)
            return day == 1;
        return false;
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
    public static int GetEstimatedSupplyNum(Supply bucket)
    {
        switch (bucket)
        {
            case Nonexistent: //Strong peaking today
                return -15;
            case Insufficient: //Either weak or strong-peaking tomorrow? Assume weak
                return -4;
            case Sufficient: //Weak peaked in the past?
                return 2;
            case Surplus: //Weak peaked in the past and we made 12?
                return 14;
            default: //I don't even know what this means but we made a bunch
                return 16;
        }
    }

    public static int GetEstimatedSupplyTomorrow(Supply today)
    {
        switch (today)
        {
            case Nonexistent: //Strong peaking today
                return 0;
            case Insufficient: //Weak peaking today
                return 2;
            case Sufficient: //Weak peaked in the past?
                return 2;
            case Surplus: //Weak peaked in the past and we made 12?
                return 14;
            default: //I don't even know what this means but we made a bunch
                return 16;
        }
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
        if (diff < 0)
            return Increasing;
        if (diff == 0)
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
