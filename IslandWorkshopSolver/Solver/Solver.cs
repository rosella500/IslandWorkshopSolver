using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;

namespace IslandWorkshopSolver.Solver;
using static PeakCycle;
public class Solver
{
    public static int WORKSHOP_BONUS = 120;
    public static int GROOVE_MAX = 35;

    public static List<ItemInfo> Items = new List<ItemInfo>();

    public static int TotalGross;
    public static int TotalNet;
    public static bool Rested;

    public static int GroovePerFullDay = 40;
    public static int GroovePerPartDay = 20;
    private static int IslandRank = 10;
    public static double MaterialWeight = 0.5;
    public static CSVImporter Importer = new CSVImporter();
    public static int Week = -1;
    private static int InitStep = 0;
    public static int CurrentDay = -1;
    public static Configuration Config = new Configuration();
    private static Window? Window;
    public static Dictionary<int, (CycleSchedule schedule, int value)> SchedulesPerDay = new Dictionary<int, (CycleSchedule schedule, int value)>();

    public static void Init(Configuration newConfig, Window window)
    {
        Config = newConfig;
        Window = window;
        MaterialWeight = Config.materialValue;
        WORKSHOP_BONUS = Config.workshopBonus;
        GROOVE_MAX = Config.maxGroove;
        IslandRank = Config.islandRank;

        if (InitStep != 0 && (CurrentDay != GetCurrentDay() || Week != GetCurrentWeek()))
        {
            DalamudPlugins.Chat.PrintError("New day detected. Closing workshop solver window");
            InitStep = 0;
            window.IsOpen = false;
        }

        if (InitStep != 0)
            return;

        Week = GetCurrentWeek();
        CurrentDay = GetCurrentDay();
        Config.day = CurrentDay;
        Config.Save();
        try
        {
            Importer = new CSVImporter(Config.rootPath, Week);
            InitStep = 1;
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error importing file :" + e.Message + "\n" + e.StackTrace);
        }
    }

    public static void InitAfterWritingTodaysData()
    {
        if (InitStep != 1)
            return;

        TotalGross = 0;
        TotalNet = 0;
        Rested = false;

        int dayToSolve = CurrentDay + 1;

        SetInitialFromCSV();
        for (int i = 0; i < CurrentDay; i++)
            SetObservedFromCSV(i);

        if (!SetObservedFromCSV(CurrentDay))
        {
            DalamudPlugins.Chat.PrintError("Don't have supply info from current day. Make sure you've viewed the Supply/Demand chart and reopen the window.");
            InitStep = 0;
            Window!.IsOpen = false;
            Init(Config, Window!);
            return;
        }

        for (int summary = 1; summary < Importer.endDays.Count && summary <= CurrentDay; summary++)
        {
            var prevDaySummary = Importer.endDays[summary];
            PluginLog.LogDebug("previous day summary: " + prevDaySummary);
            if (prevDaySummary.crafts != null)
            {
                var twoDaysAgo = Importer.endDays[summary - 1];
                CycleSchedule yesterdaySchedule = new CycleSchedule(summary, twoDaysAgo.endingGroove);
                yesterdaySchedule.SetForAllWorkshops(prevDaySummary.crafts);
                int gross = yesterdaySchedule.GetValue();

                int net = gross - yesterdaySchedule.GetMaterialCost();
                if (gross != prevDaySummary.endingGross)
                {
                    PluginLog.LogDebug("Writing summary to file. New gross: " + gross);

                    Importer.WriteEndDay(summary, prevDaySummary.endingGroove, gross, net, prevDaySummary.crafts);
                }
                TotalGross += gross;
                TotalNet += net;
                prevDaySummary.endingGross = gross;
                prevDaySummary.endingNet = net;
                prevDaySummary.valuesPerCraft = yesterdaySchedule.cowriesPerHour;
            }
        }
        Rested = false;
        for (int i = 1; i < Importer.endDays.Count && i <= CurrentDay; i++)
        {
            if (Importer.endDays[i].endingGross == 0)
            {
                PluginLog.LogInformation("Rest day found on day " + (i + 1));
                Rested = true;
            }
        }
        InitStep = 2;

    }
    static public List<(int, SuggestedSchedules?)>? RunSolver(Dictionary<int, int> inventory)
    {
        if (InitStep != 2)
        {
            PluginLog.LogError("Trying to run solver before solver initiated");
            return null;
        }

        //TODO: Figure out how to handle D2 because no one's going to craft things D1 to find out
        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        int dayToSolve = CurrentDay + 1;


        List<(int, SuggestedSchedules?)> toReturn = new List<(int, SuggestedSchedules?)>();
        if (dayToSolve == 1)
        {
            toReturn.Add((0, null));

            SetDay(new List<Item>(), 0);
        }

        if (dayToSolve < 4)
        {
            Dictionary<WorkshopSchedule, int> safeSchedules = GetSuggestedSchedules(dayToSolve, -1, null);

            //This is faster than just using LINQ, lol
            var bestSched = GetBestSchedule(safeSchedules);

            if (!Rested || !Config.enforceRestDays)
                AddRestDayValue(safeSchedules, GetWorstFutureDay(bestSched, dayToSolve));


            toReturn.Add((dayToSolve, new SuggestedSchedules(safeSchedules, Config.onlySuggestMaterialsOwned, inventory)));
        }
        else if (dayToSolve < 7)
        {
            if (Importer.NeedCurrentPeaks())
                Importer.WriteCurrentPeaks(Week);

            if (dayToSolve == 4)
                toReturn.AddRange(GetLastThreeDays(Config.onlySuggestMaterialsOwned, inventory));
            else if (dayToSolve == 5)
                toReturn.AddRange(GetLastTwoDays(Config.onlySuggestMaterialsOwned, inventory));
            else if (Rested || !Config.enforceRestDays)
                toReturn.Add((dayToSolve, new SuggestedSchedules(GetSuggestedSchedules(dayToSolve, -1, null), Config.onlySuggestMaterialsOwned, inventory)));
            else
                toReturn.Add((dayToSolve, null));
        }
        //Technically speaking we can log in on D7 but there's nothing we can really do

        PluginLog.LogInformation("Took {0} ms to calculate suggestions for day {1}. Suggestions length: {2}", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time, dayToSolve + 1, toReturn.Count);

        return toReturn;

    }

    

    public static void AddRestDayValue(Dictionary<WorkshopSchedule, int> safeSchedules, int restValue)
    {
        safeSchedules.Add(new WorkshopSchedule(new List<Item>()), restValue);
    }

    public static void AddUnknownD2(Item item)
    {
        if (Config.unknownD2Items == null)
            Config.unknownD2Items = new Dictionary<Item, bool>();
        if (!Config.unknownD2Items.ContainsKey(item))
            Config.unknownD2Items.Add(item, false);
    }

    public static string GetD2PeakDesc()
    {
        if (CurrentDay == 0 && Config.unknownD2Items != null)
        {
            //Set each peak according to config
            foreach (var item in Config.unknownD2Items)
            {
                Items[(int)(item.Key)].peak = item.Value ? Cycle2Strong : Cycle2Weak;
            }
        }

        int weak = 0;
        int strong = 0;
        foreach (var item in Items)
        {
            if (item.peak == Cycle2Strong)
                strong++;
            else if (item.peak == Cycle2Weak)
                weak++;
        }
        return "D2: " + weak + "/4 weak peaks and " + strong + "/4 strong peaks";
    }

    public static IOrderedEnumerable<KeyValuePair<Material, int>>? GetScheduledMatsNeeded()
    {
        Dictionary<Material, int> mats = new Dictionary<Material, int>();
        foreach (var schedule in SchedulesPerDay)
        {
            if(schedule.Key>CurrentDay)
                GetAllMatsForSchedule(schedule.Value.schedule.workshops[0], mats);
        }

        if (mats.Count == 0)
            return null;

        var orderedDict = mats.OrderByDescending(mat => { RareMaterialHelper.GetMaterialValue(mat.Key, out int value); return value; });
        return orderedDict;
    }

    public static void GetAllMatsForSchedule(WorkshopSchedule schedule, in Dictionary<Material, int> mats)
    {
        foreach (var item in schedule.GetItems())
        {
            foreach (var mat in Items[(int)item].materialsRequired)
            {
                if (mats.ContainsKey(mat.Key))
                    mats[mat.Key] += mat.Value * 3;
                else
                    mats.Add(mat.Key, mat.Value * 3);
            }
        }
    }

    
    public static List<(int, SuggestedSchedules?)> GetLastTwoDays(bool removeCantMake, Dictionary<int, int> inventory)
    {
        HashSet<Item>? reservedFor6 = null;
        int startingGroove = GetEndingGrooveForDay(CurrentDay);
        bool sixSet = false;
        bool sevenSet = false;
        int dayRested = -1;

        if (SchedulesPerDay.TryGetValue(5, out var schedule6))
        {
            sixSet = true;
            if (schedule6.schedule.workshops[0].GetItems().Count == 0)
                dayRested = 5;
        }

        if (SchedulesPerDay.TryGetValue(6, out var schedule7))
        {
            sevenSet = true;
            List<Item> items7 = schedule7.schedule.workshops[0].GetItems();
            if (sixSet)
            {
                SetDay(items7, 6); //Recalculate with 6's groove
            }
            if (schedule7.schedule.workshops[0].GetItems().Count == 0)
                dayRested = 7;
            reservedFor6 = new HashSet<Item>(schedule7.schedule.workshops[0].GetItems());
        }

        List<Dictionary<WorkshopSchedule, int>> initialSchedules = new List<Dictionary<WorkshopSchedule, int>>
        {
            GetSuggestedSchedules(5, startingGroove, reservedFor6),
            GetSuggestedSchedules(6, startingGroove, null)
        };
        List<KeyValuePair<WorkshopSchedule, int>> initialBests = new List<KeyValuePair<WorkshopSchedule, int>>
        {
            GetBestSchedule(initialSchedules[0]),
            GetBestSchedule(initialSchedules[1])
        };

        if (!Rested || !Config.enforceRestDays)
        {
            if (dayRested == -1)
            {
                if(Config.enforceRestDays)
                {
                    if (sevenSet) //Must rest 6
                        initialSchedules[0].Clear();
                    if (sixSet) //Must rest 7
                        initialSchedules[1].Clear();
                }

                AddRestDayValue(initialSchedules[0], initialBests[1].Value);
                AddRestDayValue(initialSchedules[1], initialBests[0].Value);
            }
            else if (dayRested == 5)
                AddRestDayValue(initialSchedules[0], initialBests[1].Value);
            else if (dayRested == 6)
                AddRestDayValue(initialSchedules[1], initialBests[0].Value);
        }

        List<(int, SuggestedSchedules?)> suggested = new List<(int, SuggestedSchedules?)>();
        suggested.Add((5, new SuggestedSchedules(initialSchedules[0], removeCantMake, inventory)));
        suggested.Add((6, new SuggestedSchedules(initialSchedules[1], removeCantMake, inventory)));
        return suggested;
    }
    public static List<(int, SuggestedSchedules?)> GetLastThreeDays(bool removeCantMake, Dictionary<int, int> inventory)
    {
        HashSet<Item> reservedFor6 = new HashSet<Item>();
        HashSet<Item> reservedFor5 = new HashSet<Item>();
        int startingGroove = GetEndingGrooveForDay(CurrentDay);
        bool fiveSet = false;
        bool sixSet = false;
        bool sevenSet = false;
        int dayRested = -1;

        if (SchedulesPerDay.TryGetValue(4, out var schedule5))
        {
            fiveSet = true;
            if (schedule5.schedule.workshops[0].GetItems().Count == 0)
                dayRested = 4;
        }

        if (SchedulesPerDay.TryGetValue(5, out var schedule6))
        {
            List<Item> items6 = schedule6.schedule.workshops[0].GetItems();
            if (fiveSet)
            {
                SetDay(items6, 5); //Recalculate with 5's groove
                sixSet = true;
            }
            if (schedule6.schedule.workshops[0].GetItems().Count == 0)
                dayRested = 5;

            reservedFor5.UnionWith(schedule6.schedule.workshops[0].GetItems());
        }

        if (SchedulesPerDay.TryGetValue(6, out var schedule7))
        {
            sevenSet = true;
            List<Item> items7 = schedule7.schedule.workshops[0].GetItems();
            if (sixSet)
            {
                SetDay(items7, 6); //Recalculate with 6's groove
            }
            if (schedule7.schedule.workshops[0].GetItems().Count == 0)
                dayRested = 7;
            reservedFor5.UnionWith(schedule7.schedule.workshops[0].GetItems());
            reservedFor6.UnionWith(schedule7.schedule.workshops[0].GetItems());
        }

        List<Dictionary<WorkshopSchedule, int>> initialSchedules = new List<Dictionary<WorkshopSchedule, int>>
        {
            GetSuggestedSchedules(4, startingGroove, reservedFor5),
            GetSuggestedSchedules(5, startingGroove, reservedFor6),
            GetSuggestedSchedules(6, startingGroove, null)
        };
        List<KeyValuePair<WorkshopSchedule, int>> initialBests = new List<KeyValuePair<WorkshopSchedule, int>>
        {
            GetBestSchedule(initialSchedules[0]),
            GetBestSchedule(initialSchedules[1]),
            GetBestSchedule(initialSchedules[2])
        };

        if (!Rested || !Config.enforceRestDays)
        {
            if (dayRested == -1 || !Config.enforceRestDays)
            {
                if(Config.enforceRestDays)
                {
                    if (sixSet && sevenSet) //Must rest 5
                        initialSchedules[0].Clear();
                    if (sevenSet && fiveSet) //Must rest 6
                        initialSchedules[1].Clear();
                    if (fiveSet && sixSet) //Must rest 7
                        initialSchedules[2].Clear();
                }

                AddRestDayValue(initialSchedules[0], Math.Min(initialBests[1].Value, initialBests[2].Value));
                AddRestDayValue(initialSchedules[1], Math.Min(initialBests[0].Value, initialBests[2].Value));
                AddRestDayValue(initialSchedules[2], Math.Min(initialBests[1].Value, initialBests[0].Value));
            }
            else if (dayRested == 4)
                AddRestDayValue(initialSchedules[0], Math.Min(initialBests[1].Value, initialBests[2].Value));
            else if (dayRested == 5)
                AddRestDayValue(initialSchedules[1], Math.Min(initialBests[0].Value, initialBests[2].Value));
            else if (dayRested == 6)
                AddRestDayValue(initialSchedules[2], Math.Min(initialBests[1].Value, initialBests[0].Value));
        }

        List<(int, SuggestedSchedules?)> suggested = new List<(int, SuggestedSchedules?)>();
        suggested.Add((4, new SuggestedSchedules(initialSchedules[0], removeCantMake, inventory)));
        suggested.Add((5, new SuggestedSchedules(initialSchedules[1], removeCantMake, inventory)));
        suggested.Add((6, new SuggestedSchedules(initialSchedules[2], removeCantMake, inventory)));
        return suggested;
    }

    private static int GetWorstFutureDay(KeyValuePair<WorkshopSchedule, int> rec, int day)
    {
        int worstInFuture = 99999;
        PluginLog.LogDebug("Comparing d" + (day + 1) + " (" + rec.Value + ") to worst-case future days");
        HashSet<Item> reservedSet = new HashSet<Item>(rec.Key.GetItems());
        for (int d = day + 1; d < 7; d++)
        {
            KeyValuePair<WorkshopSchedule, int> solution;
            if (day == 3 && d == 4) //We have a lot of info about this specific pair so we might as well use it
                solution = GetD5EV();
            else
                solution = GetBestSchedule(d, reservedSet, false);

            if(solution.Key!=null)
            {

                PluginLog.LogDebug("Day " + (d + 1) + ", crafts: " + String.Join(", ", solution.Key.GetItems()) + " value: " + solution.Value);
                worstInFuture = Math.Min(worstInFuture, solution.Value);
                reservedSet.UnionWith(solution.Key.GetItems());
            }

        }
            PluginLog.LogDebug("Worst future day: " + worstInFuture);

        return worstInFuture;
    }

    //Specifically for comparing D4 to D5
    public static KeyValuePair<WorkshopSchedule, int> GetD5EV()
    {
        KeyValuePair<WorkshopSchedule, int> solution = GetBestSchedule(4, null);
            //PluginLog.LogVerbose("Testing against D5 solution " + solution.Key.getItems());
        List<ItemInfo> c5Peaks = new List<ItemInfo>();
        foreach (Item item in solution.Key.GetItems())
            if (Items[(int)item].peak == Cycle5 && !c5Peaks.Contains(Items[(int)item]))
                c5Peaks.Add(Items[(int)item]);
        int sum = solution.Value;
        int permutations = (int)Math.Pow(2, c5Peaks.Count);

            //PluginLog.LogVerbose("C5 peaks: " + c5Peaks.Count + ", permutations: " + permutations);

        for (int p = 1; p < permutations; p++)
        {
            for (int i = 0; i < c5Peaks.Count; i++)
            {
                bool strong = ((p) & (1 << i)) != 0; //I can't believe I'm using a bitwise and
                    //PluginLog.LogVerbose("Checking permutation " + p + " for item " + c5Peaks[i].item + " " + (strong ? "strong" : "weak"));
                if (strong)
                    c5Peaks[i].peak = Cycle5Strong;
                else
                    c5Peaks[i].peak = Cycle5Weak;
            }

            int toAdd = solution.Key.GetValueWithGrooveEstimate(4, GetEndingGrooveForDay(CurrentDay));
                //PluginLog.LogVerbose("Permutation " + p + " has value " + toAdd);
            sum += toAdd;
        }

            //PluginLog.LogVerbose("Sum: " + sum + " average: " + sum / permutations);
        sum /= permutations;
        KeyValuePair<WorkshopSchedule, int> newSolution = new KeyValuePair<WorkshopSchedule, int>(solution.Key, sum);


        foreach (ItemInfo item in c5Peaks)
        {
            item.peak = Cycle5; //Set back to normal
        }
        return newSolution;
    }


    private static int GetEndingGrooveForDay(int day)
    {
        if (Importer.endDays.Count > day && day >=0 && day<=CurrentDay)
            return Importer.endDays[day].endingGroove;
        else if(SchedulesPerDay.TryGetValue(day, out var schedule))
        {
            PluginLog.LogDebug("Getting ending groove from scheduled day " +day+": " + schedule.schedule.endingGroove);
            return schedule.schedule.endingGroove;
        }
        PluginLog.LogDebug("Can't find day in summaries or schedules. Returning 0");

        return 0;
    }

    public static void SetDay(List<Item> crafts, int day)
    {
        if (day != 0)
            PluginLog.LogInformation("Day {0}, crafts: {1}", day+1, crafts);


        CycleSchedule schedule = new CycleSchedule(day, 0);
        schedule.SetForAllWorkshops(crafts);

        if(SchedulesPerDay.TryGetValue(day, out var previousSchedule))
        {
            TotalGross -= previousSchedule.value;
            TotalNet -= (previousSchedule.value - previousSchedule.schedule.GetMaterialCost());
            SchedulesPerDay.Remove(day);
            foreach (var item in Items)
                item.SetCrafted(0, day);
        }

        int zeroGrooveValue = schedule.GetValue();
        int groove = GetEndingGrooveForDay(day - 1);
        schedule.startingGroove = groove;
        int gross = schedule.GetValue();
        TotalGross += gross;

        int net = gross - schedule.GetMaterialCost();
        TotalNet += net;
        groove = schedule.endingGroove;

        if (day != 0)
            PluginLog.LogInformation("day {0} total, 0 groove: {1}. Starting groove {2}: {3}, net {4}.", day + 1, zeroGrooveValue, schedule.startingGroove, gross, net);

        foreach (var kvp in schedule.numCrafted)
        {
            Items[(int)kvp.Key].SetCrafted(kvp.Value, day);
        }
        SchedulesPerDay.Add(day, (schedule, gross));

        if (schedule.HasAnyUnsurePeaks())
            Importer.WriteEndDay(day, groove, -1, -1, crafts);
        else
            Importer.WriteEndDay(day, groove, gross, net, crafts);

        //Don't think we should do this
        //updateRestedStatus();
    }
    private static Dictionary<WorkshopSchedule, int> GetSuggestedSchedules(int day, int startingGroove, HashSet<Item>? reservedForLater, bool allowAllOthers = true)
    {
        if (startingGroove == -1)
            startingGroove = GetEndingGrooveForDay(CurrentDay - 1);

        var fourHour = new List<ItemInfo>();
        var eightHour = new List<ItemInfo>();
        var sixHour = new List<ItemInfo>();

        if (reservedForLater == null || reservedForLater.Count == 0)
            allowAllOthers = false;

        foreach (ItemInfo item in Items)
        {
            List<ItemInfo>? bucket = null;

            if (reservedForLater != null && reservedForLater.Contains(item.item))
                continue;

            if (item.time == 4 && item.rankUnlocked <= IslandRank && (allowAllOthers || item.PeaksOnOrBeforeDay(day, true)))
                bucket = fourHour;
            else if (item.time == 6 && item.rankUnlocked <= IslandRank && (allowAllOthers || item.PeaksOnOrBeforeDay(day, false)))
                bucket = sixHour;
            else if (item.time == 8 && item.rankUnlocked <= IslandRank && (allowAllOthers || item.PeaksOnOrBeforeDay(day, false)))
                bucket = eightHour;

            if (bucket != null)
                bucket.Add(item);
        }


        Dictionary<WorkshopSchedule, int> safeSchedules = new Dictionary<WorkshopSchedule, int>();

        //Find schedules based on 8-hour crafts
        var eightEnum = eightHour.GetEnumerator();
        while (eightEnum.MoveNext())
        {
            var topItem = eightEnum.Current;
            //PluginLog.LogVerbose("Building schedule around : " + topItem.item + ", peak: " + topItem.peak);


            List<ItemInfo> eightMatches = new List<ItemInfo>();
            //8-8-8
            var eightMatchEnum = eightHour.GetEnumerator();
            while (eightMatchEnum.MoveNext())
            {
                if (!eightMatchEnum.Current.GetsEfficiencyBonus(topItem))
                    continue;
                eightMatches.Add(eightMatchEnum.Current);
                AddToScheduleMap(new List<Item> { topItem.item, eightMatchEnum.Current.item, topItem.item }, day, safeSchedules, startingGroove);
            }

            //4-8-4-8 and 4-4-4-4-8
            var firstFourMatchEnum = fourHour.GetEnumerator();
            while (firstFourMatchEnum.MoveNext())
            {
                if (!firstFourMatchEnum.Current.GetsEfficiencyBonus(topItem))
                    continue;

                    //PluginLog.LogVerbose("Found 4hr match, matching with " + firstFourMatchEnum.Current.item);

                var secondFourMatchEnum = fourHour.GetEnumerator();
                while (secondFourMatchEnum.MoveNext())
                {
                    //PluginLog.LogVerbose("Checking potential 4hr match: " + secondFourMatchEnum.Current.item);
                    AddScheduleIfEfficient(secondFourMatchEnum.Current, topItem,
                        new List<Item> { firstFourMatchEnum.Current.item, topItem.item, secondFourMatchEnum.Current.item, topItem.item },
                        day, safeSchedules, startingGroove);


                    if (!secondFourMatchEnum.Current.GetsEfficiencyBonus(firstFourMatchEnum.Current))
                        continue;

                    //4-4-8-8
                    foreach(var eightMatch in eightMatches)
                        AddToScheduleMap(new List<Item> { secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item, eightMatch.item },
                            day, safeSchedules, startingGroove);

                    var thirdFourMatchEnum = fourHour.GetEnumerator();
                    while (thirdFourMatchEnum.MoveNext())
                    {
                        if (!secondFourMatchEnum.Current.GetsEfficiencyBonus(thirdFourMatchEnum.Current))
                            continue;


                        var fourthFourMatchEnum = fourHour.GetEnumerator();
                        while (fourthFourMatchEnum.MoveNext())
                        {
                            AddScheduleIfEfficient(fourthFourMatchEnum.Current, thirdFourMatchEnum.Current,
                                new List<Item> { fourthFourMatchEnum.Current.item, thirdFourMatchEnum.Current.item, secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item },
                                day, safeSchedules, startingGroove);
                        }
                    }
                }
            }

            //4-6-8-6
            var sixMatchEnum = sixHour.GetEnumerator();
            while (sixMatchEnum.MoveNext())
            {
                var sixHourMatch = sixMatchEnum.Current;
                if (!sixHourMatch.GetsEfficiencyBonus(topItem))
                    continue;
                var fourMatchEnum = fourHour.GetEnumerator();
                while (fourMatchEnum.MoveNext())
                {
                    AddScheduleIfEfficient(fourMatchEnum.Current, sixHourMatch,
                        new List<Item> { fourMatchEnum.Current.item, sixHourMatch.item, topItem.item, sixHourMatch.item },
                        day, safeSchedules, startingGroove);
                }
            }
        }

        //Find schedules based on 6-hour crafts
        var sixEnum = sixHour.GetEnumerator();
        while (sixEnum.MoveNext())
        {
            var topItem = sixEnum.Current;

                //PluginLog.LogVerbose("Building schedule around : " + topItem.item);


            //6-6-6-6
            HashSet<ItemInfo> sixMatches = new HashSet<ItemInfo>();
            var sixMatchEnum = sixHour.GetEnumerator();
            while (sixMatchEnum.MoveNext())
            {
                if (!sixMatchEnum.Current.GetsEfficiencyBonus(topItem))
                    continue;
                sixMatches.Add(sixMatchEnum.Current);
            }
            foreach (ItemInfo firstSix in sixMatches)
            {
                foreach (ItemInfo secondSix in sixMatches)
                {
                        //PluginLog.LogVerbose("Adding 6-6-6-6 schedule made out of helpers " + firstSix.item + ", " + secondSix.item + ", and top item: " + topItem.item);
                    AddToScheduleMap(new List<Item> { secondSix.item, topItem.item, firstSix.item, topItem.item },
                    day, safeSchedules, startingGroove);
                }
            }

            //4-4-4-4-6 and 4-4-6-4-6
            var firstFourMatchEnum = fourHour.GetEnumerator();
            while (firstFourMatchEnum.MoveNext())
            {
                if (!firstFourMatchEnum.Current.GetsEfficiencyBonus(topItem))
                    continue;


                var sixFourMatchEnum = sixHour.GetEnumerator();
                //4-6-6-6
                while (sixFourMatchEnum.MoveNext())
                {
                    AddToScheduleMap(new List<Item> { firstFourMatchEnum.Current.item, topItem.item, sixFourMatchEnum.Current.item, topItem.item },
                        day, safeSchedules, startingGroove);
                }

                var secondFourMatchEnum = fourHour.GetEnumerator();
                while (secondFourMatchEnum.MoveNext())
                {
                    if (!secondFourMatchEnum.Current.GetsEfficiencyBonus(firstFourMatchEnum.Current))
                        continue;

                    AddScheduleIfEfficient(secondFourMatchEnum.Current, topItem,
                        new List<Item> { firstFourMatchEnum.Current.item, secondFourMatchEnum.Current.item, topItem.item, firstFourMatchEnum.Current.item, topItem.item },
                        day, safeSchedules, startingGroove);
                    AddToScheduleMap(new List<Item> { secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item, firstFourMatchEnum.Current.item, topItem.item },
                        day, safeSchedules, startingGroove);

                    var thirdFourMatchEnum = fourHour.GetEnumerator();
                    while (thirdFourMatchEnum.MoveNext())
                    {
                        if (!secondFourMatchEnum.Current.GetsEfficiencyBonus(thirdFourMatchEnum.Current))
                            continue;
                        var fourthFourMatchEnum = fourHour.GetEnumerator();
                        while (fourthFourMatchEnum.MoveNext())
                        {
                            AddScheduleIfEfficient(fourthFourMatchEnum.Current, thirdFourMatchEnum.Current,
                                new List<Item> { fourthFourMatchEnum.Current.item, thirdFourMatchEnum.Current.item, secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item },
                                day, safeSchedules, startingGroove);
                        }
                    }
                }
            }
        }
        
        return safeSchedules;
    }

    private static KeyValuePair<WorkshopSchedule, int> GetBestSchedule(int day, HashSet<Item>? reservedForLater, bool allowAllOthers = true)
    {
        var suggested = GetSuggestedSchedules(day, -1, reservedForLater, allowAllOthers);
        
        return GetBestSchedule(suggested);
    }

    private static KeyValuePair<WorkshopSchedule, int> GetBestSchedule(Dictionary<WorkshopSchedule, int> schedulesAvailable)
    {
        KeyValuePair<WorkshopSchedule, int> bestSched = schedulesAvailable.First();
        foreach (var sched in schedulesAvailable)
        {
            if (sched.Value > bestSched.Value) bestSched = sched;
        }
        return bestSched;
    }

    public static bool AddScheduleIfEfficient(ItemInfo newItem, ItemInfo origItem, List<Item> scheduledItems, int day, Dictionary<WorkshopSchedule, int> safeSchedules, int startingGroove)
    {
        if (!newItem.GetsEfficiencyBonus(origItem))
            return false;


        AddToScheduleMap(scheduledItems, day, safeSchedules, startingGroove);
        return true;
    }

    private static int AddToScheduleMap(List<Item> list, int day, Dictionary<WorkshopSchedule, int> safeSchedules, int startingGroove)
    {
        WorkshopSchedule workshop = new WorkshopSchedule(list);

        int value = workshop.GetValueWithGrooveEstimate(day, startingGroove);
        //Only add if we don't already have one with this schedule or ours is better
        if(safeSchedules.TryGetValue(workshop, out int oldValue))
        {
                //PluginLog.LogVerbose("Found workshop in safe schedules with rare mats: " + String.Join(", ", workshop.rareMaterialsRequired));
        }
        else
        {
                //PluginLog.LogVerbose("Can't find workshop schedule out of "+safeSchedules.Count+" with rare mats: " + String.Join(", ", workshop.rareMaterialsRequired));
            oldValue = -1;
        }

        if (oldValue < value)
        {
            if (oldValue != -1)
                //PluginLog.LogVerbose("Replacing schedule with mats " + String.Join(", ",workshop.rareMaterialsRequired) + " with " + String.Join(", ",list) + " because " + value + " is higher than " + oldValue);
            safeSchedules.Remove(workshop); //It doesn't seem to update the key when updating the value, so we delete the key first
            safeSchedules.Add(workshop, value);
        }
        else
        {
                //PluginLog.LogVerbose("Not replacing schedule with mats " + String.Join(", ",workshop.rareMaterialsRequired) + " with " + String.Join(", ",list) + " because " + value + " is lower than " + oldValue);

                value = 0;
        }

        return value;

    }

    private static void SetInitialFromCSV()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].SetInitialData(Importer.currentPopularity[i], Importer.lastWeekPeaks[i]);
        }
    }

    private static bool SetObservedFromCSV(int day)
    {
        bool hasDaySummary = day < Importer.endDays.Count;
        bool hasCurrentDay = day == 6;

        for (int i = 0; i < Importer.observedSupplies.Count; i++)
        {
            if (day < 6 && Importer.observedSupplies[i].ContainsKey(day))
            {
                hasCurrentDay = true;
                ObservedSupply ob = Importer.observedSupplies[i][day];
                int observedHour = 0;
                if (Importer.observedSupplyHours.Count > day)
                    observedHour = Importer.observedSupplyHours[day];
                Items[i].AddObservedDay(ob, day, observedHour);
            }
            else if (day == 6)
                Items[i].SetPeakBasedOnObserved(0);

            if (hasDaySummary && Importer.endDays[day].NumCraftedCount() > i)
                Items[i].SetCrafted(Importer.endDays[day].GetCrafted(i), day);
            else
                Items[i].SetCrafted(0, day);
        }
        return hasCurrentDay;
    }

    public static void InitItemsFromGameData(List<ItemInfo> items)
    {
        Items = items;
    }
    /*public static void DefaultInitItems()
    {
        Items = new List<ItemInfo>();
        Items.Add(new ItemInfo(Potion, Concoctions, Invalid, 28, 4, 1, null));
        Items.Add(new ItemInfo(Firesand, Concoctions, UnburiedTreasures, 28, 4, 1, null));
        Items.Add(new ItemInfo(WoodenChair, Furnishings, Woodworks, 42, 6, 1, null));
        Items.Add(new ItemInfo(GrilledClam, Foodstuffs, MarineMerchandise, 28, 4, 1, null));
        Items.Add(new ItemInfo(Necklace, Accessories, Woodworks, 28, 4, 1, null));
        Items.Add(new ItemInfo(CoralRing, Accessories, MarineMerchandise, 42, 6, 1, null));
        Items.Add(new ItemInfo(Barbut, Attire, Metalworks, 42, 6, 1, null));
        Items.Add(new ItemInfo(Macuahuitl, Arms, Woodworks, 42, 6, 1, null));
        Items.Add(new ItemInfo(Sauerkraut, PreservedFood, Invalid, 40, 4, 1, new Dictionary<Material, int>() { { Cabbage, 1 } }));
        Items.Add(new ItemInfo(BakedPumpkin, Foodstuffs, Invalid, 40, 4, 1, new Dictionary<Material, int>() { { Pumpkin, 1 } }));
        Items.Add(new ItemInfo(Tunic, Attire, Textiles, 72, 6, 1, new Dictionary<Material, int>() { { Fleece, 2 } }));
        Items.Add(new ItemInfo(CulinaryKnife, Sundries, CreatureCreations, 44, 4, 1, new Dictionary<Material, int>() { { Claw, 1 } }));
        Items.Add(new ItemInfo(Brush, Sundries, Woodworks, 44, 4, 1, new Dictionary<Material, int>() { { Fur, 1 } }));
        Items.Add(new ItemInfo(BoiledEgg, Foodstuffs, CreatureCreations, 44, 4, 1, new Dictionary<Material, int>() { { Egg, 1 } }));
        Items.Add(new ItemInfo(Hora, Arms, CreatureCreations, 72, 6, 1, new Dictionary<Material, int>() { { Carapace, 2 } }));
        Items.Add(new ItemInfo(Earrings, Accessories, CreatureCreations, 44, 4, 1, new Dictionary<Material, int>() { { Fang, 1 } }));
        Items.Add(new ItemInfo(Butter, Ingredients, CreatureCreations, 44, 4, 1, new Dictionary<Material, int>() { { Milk, 1 } }));
        Items.Add(new ItemInfo(BrickCounter, Furnishings, UnburiedTreasures, 48, 6, 5, null));
        Items.Add(new ItemInfo(BronzeSheep, Furnishings, Metalworks, 64, 8, 5, null));
        Items.Add(new ItemInfo(GrowthFormula, Concoctions, Invalid, 136, 8, 5, new Dictionary<Material, int>() { { Alyssum, 2 } }));
        Items.Add(new ItemInfo(GarnetRapier, Arms, UnburiedTreasures, 136, 8, 5, new Dictionary<Material, int>() { { Garnet, 2 } }));
        Items.Add(new ItemInfo(SpruceRoundShield, Attire, Woodworks, 136, 8, 5, new Dictionary<Material, int>() { { Spruce, 2 } }));
        Items.Add(new ItemInfo(SharkOil, Sundries, MarineMerchandise, 136, 8, 5, new Dictionary<Material, int>() { { Shark, 2 } }));
        Items.Add(new ItemInfo(SilverEarCuffs, Accessories, Metalworks, 136, 8, 5, new Dictionary<Material, int>() { { Silver, 2 } }));
        Items.Add(new ItemInfo(SweetPopoto, Confections, Invalid, 72, 6, 5, new Dictionary<Material, int>() { { Popoto, 2 }, { Milk, 1 } }));
        Items.Add(new ItemInfo(ParsnipSalad, Foodstuffs, Invalid, 48, 4, 5, new Dictionary<Material, int>() { { Parsnip, 2 } }));
        Items.Add(new ItemInfo(Caramels, Confections, Invalid, 81, 6, 6, new Dictionary<Material, int>() { { Milk, 2 } }));
        Items.Add(new ItemInfo(Ribbon, Accessories, Textiles, 54, 6, 6, null));
        Items.Add(new ItemInfo(Rope, Sundries, Textiles, 36, 4, 6, null));
        Items.Add(new ItemInfo(CavaliersHat, Attire, Textiles, 81, 6, 6, new Dictionary<Material, int>() { { Feather, 2 } }));
        Items.Add(new ItemInfo(Item.Horn, Sundries, CreatureCreations, 81, 6, 6, new Dictionary<Material, int>() { { Material.Horn, 2 } }));
        Items.Add(new ItemInfo(SaltCod, PreservedFood, MarineMerchandise, 54, 6, 7, null));
        Items.Add(new ItemInfo(SquidInk, Ingredients, MarineMerchandise, 36, 4, 7, null));
        Items.Add(new ItemInfo(EssentialDraught, Concoctions, MarineMerchandise, 54, 6, 7, null));
        Items.Add(new ItemInfo(Jam, Ingredients, Invalid, 78, 6, 7, new Dictionary<Material, int>() { { Isleberry, 3 } }));
        Items.Add(new ItemInfo(TomatoRelish, Ingredients, Invalid, 52, 4, 7, new Dictionary<Material, int>() { { Tomato, 2 } }));
        Items.Add(new ItemInfo(OnionSoup, Foodstuffs, Invalid, 78, 6, 7, new Dictionary<Material, int>() { { Onion, 3 } }));
        Items.Add(new ItemInfo(Pie, Confections, MarineMerchandise, 78, 6, 7, new Dictionary<Material, int>() { { Wheat, 3 } }));
        Items.Add(new ItemInfo(CornFlakes, PreservedFood, Invalid, 52, 4, 7, new Dictionary<Material, int>() { { Corn, 2 } }));
        Items.Add(new ItemInfo(PickledRadish, PreservedFood, Invalid, 104, 8, 7, new Dictionary<Material, int>() { { Radish, 4 } }));
        Items.Add(new ItemInfo(IronAxe, Arms, Metalworks, 72, 8, 8, null));
        Items.Add(new ItemInfo(QuartzRing, Accessories, UnburiedTreasures, 72, 8, 8, null));
        Items.Add(new ItemInfo(PorcelainVase, Sundries, UnburiedTreasures, 72, 8, 8, null));
        Items.Add(new ItemInfo(VegetableJuice, Concoctions, Invalid, 78, 6, 8, new Dictionary<Material, int>() { { Cabbage, 3 } }));
        Items.Add(new ItemInfo(PumpkinPudding, Confections, Invalid, 78, 6, 8, new Dictionary<Material, int>() { { Pumpkin, 3 }, { Egg, 1 }, { Milk, 1 } }));
        Items.Add(new ItemInfo(SheepfluffRug, Furnishings, CreatureCreations, 90, 6, 8, new Dictionary<Material, int>() { { Fleece, 3 } }));
        Items.Add(new ItemInfo(GardenScythe, Sundries, Metalworks, 90, 6, 9, new Dictionary<Material, int>() { { Claw, 3 } }));
        Items.Add(new ItemInfo(Bed, Furnishings, Textiles, 120, 8, 9, new Dictionary<Material, int>() { { Fur, 4 } }));
        Items.Add(new ItemInfo(ScaleFingers, Attire, CreatureCreations, 120, 8, 9, new Dictionary<Material, int>() { { Carapace, 4 } }));
        Items.Add(new ItemInfo(Crook, Arms, Woodworks, 120, 8, 9, new Dictionary<Material, int>() { { Fang, 4 } }));
    }*/

    public static int GetCurrentWeek()
    {
        //August 23 2022
        DateTime startOfIS = new DateTime(2022, 8, 23, 8, 0, 0, DateTimeKind.Utc);
        DateTime current = DateTime.UtcNow;

        TimeSpan timeSinceStart = (current - startOfIS);
        int week = timeSinceStart.Days / 7 + 1;
        
        PluginLog.LogDebug("Current week: {0}", week);

        return week;
    }

    public static int GetCurrentDay()
    {
        DateTime startOfIS = new DateTime(2022, 8, 23, 8, 0, 0, DateTimeKind.Utc);
        DateTime current = DateTime.UtcNow;
        TimeSpan timeSinceStart = (current - startOfIS);
        return timeSinceStart.Days % 7;
    }

    public static int GetCurrentHour()
    {
        DateTime startOfIS = new DateTime(2022, 8, 23, 8, 0, 0, DateTimeKind.Utc);
        DateTime current = DateTime.UtcNow;
        TimeSpan timeSinceStart = (current - startOfIS);
        return timeSinceStart.Hours;
    }

    public static bool WriteTodaySupply(int updatedDay, string[] products)
    {

        if (CurrentDay != GetCurrentDay() || Week != GetCurrentWeek())
        {
            CurrentDay = GetCurrentDay();
            Week = GetCurrentWeek();
            InitStep = 0;
            Init(Config, Window!);
        }

        bool needOverwrite = CurrentDay < 6 && IsProductsValid(products) && Importer.NeedToOverwriteTodayData(CurrentDay, products);
        if (needOverwrite)
            PluginLog.Warning("Found new supply info that seems to be from a later day than what we currently have. Overwriting");

        if (InitStep < 1)
        {
            PluginLog.LogError("Trying to run solver before solver initiated");
            return false;
        }
        else if (InitStep > 1 && !needOverwrite)
            return true;

        InitStep = 1;


        bool needNewWeek = Importer.NeedNewWeekData(Week);
        bool needNewData = CurrentDay < 6 && Importer.NeedNewTodayData(CurrentDay);

        if (!(needNewData || needNewWeek || needOverwrite))
            return true;


        PluginLog.LogInformation("Trying to write supply info starting with " + products[0] + ", last updated day " + (updatedDay + 1));

        if (updatedDay == CurrentDay && IsProductsValid(products))
        {
            if (needNewWeek || (CurrentDay == 0 && needOverwrite))
            {
                Importer.WriteWeekStart(products);
            }
            
            if(needNewData || needOverwrite)
            {
                Importer.WriteNewSupply(products, CurrentDay);
            }
            return true;
        }            
        return false;
        
    }

    private static bool IsProductsValid(string[] products)
    {
        if (products.Length < Items.Count)
            return false;

        int numNE = 0;
        List<int> todaySupply = new List<int>();
        for (int i = 0; i < Items.Count; i++)
        {
            string product = products[i];
            string[] productInfo = product.Split('\t');
            todaySupply.Add(int.Parse(productInfo[2]));
            if (todaySupply[i] == (int)Supply.Nonexistent)
                numNE++;

            if (CurrentDay == 0 && numNE > 0)
                return false;
            if (numNE > 4)
                return false;
        }

        //Need to check this in a second loop because we don't want those fake NEs counting as supply going down
        if (CurrentDay > 0 && Importer.observedSupplies.Count > 0 && Importer.observedSupplies[0].ContainsKey(CurrentDay - 1))
        {
            for (int i = 0; i < Items.Count; i++)
            {
                int yesterdaySupp = (int)Importer.observedSupplies[i][CurrentDay - 1].supply;

                //If any products have supply lower than it was yesterday, then this has to be a new day
                //(this should apply to at least 8 things each day and we can't have made all of them)
                if (todaySupply[i] < yesterdaySupp) 
                    return true;
            }
        }
        else //If we don't have yesterday's data then this is probably fine? 
        {
            return true;
        }

        //This uses currentDay to refer to yesterday because it'd be currentDay - 1 (for yesterday)
        //+ 1 (for displaying 1-indexed) and that's just silly
        PluginLog.LogWarning("No products have gone down in supply since day {0}'s supply data. We probably haven't updated today.", CurrentDay);
        return false;
    }
}
