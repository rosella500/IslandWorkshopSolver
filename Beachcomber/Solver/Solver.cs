using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;

namespace Beachcomber.Solver;
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
    public static int AverageDailyValue = 4044;
    public static int IslandRank = 10;
    public static int NumWorkshops = 3;
    public static double MaterialWeight = 0.5;
    public static CSVImporter Importer = new CSVImporter();
    public static int Week = -1;
    private static int InitStep = 0;
    public static int CurrentDay = -1;
    public static Configuration Config = new Configuration();
    private static Window? Window;
    public static Dictionary<int, (CycleSchedule schedule, int value)> SchedulesPerDay = new Dictionary<int, (CycleSchedule schedule, int value)>();
    private static int ItemsToReserve = 15;
    private static bool ValuePerHour = true;
    public static HashSet<Item> ReservedItems = new HashSet<Item>();
    private static Dictionary<int, int> Inventory = new Dictionary<int, int>();

    public static void Init(Configuration newConfig, Window window)
    {
        Config = newConfig;
        Window = window;
        MaterialWeight = Config.materialValue;
        WORKSHOP_BONUS = Config.workshopBonus;
        GROOVE_MAX = Config.maxGroove;
        IslandRank = Config.islandRank;
        NumWorkshops = Config.numWorkshops;

        PluginLog.Debug("Set num workshops to {0} from config ({1})", NumWorkshops, Config.numWorkshops);

        if (InitStep != 0 && (CurrentDay != GetCurrentDay() || Week != GetCurrentWeek()))
        {
            DalamudPlugins.Chat.PrintError("New day detected. Closing workshop solver window");
            InitStep = 0;
            SchedulesPerDay.Clear();
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
        SchedulesPerDay.Clear(); 
        foreach (ItemInfo item in Items)
            item.ResetForWeek();

        int dayToSolve = CurrentDay + 1;

        SetInitialFromCSV();


        bool hasCurrentDay = false;
        for (int i = 0; i <= CurrentDay; i++)
            hasCurrentDay = SetObservedFromCSV(i);

        if (Importer.HasAllPeaks()) //If we have the peaks in CSV already, just set them
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].peak = Importer.currentPeaks[i];
                PluginLog.Debug("Item {0}, final peak: {1}", Items[i].item, Items[i].peak);
            }

        }
        else if (CurrentDay == 6) //We don't get data from today but we should set the peaks anyway
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].SetPeakBasedOnObserved(0);
            }
        }
        else if (!hasCurrentDay)
        {
            DalamudPlugins.Chat.PrintError("Don't have supply info from current day. Make sure you've viewed the Supply/Demand chart and reopen the window.");
            InitStep = 0;
            Window!.IsOpen = false;
            Init(Config, Window!);
            return;
        }
        //Double check D2 peaks
        if (CurrentDay == 0 && Config.unknownD2Items != null && Config.unknownD2Items.Count > 0)
        {
            int weak = 0;
            int strong = 0;
            foreach (var item in Items)
            {
                if (item.peak == Cycle2Strong)
                    strong++;
                else if (item.peak == Cycle2Weak)
                    weak++;
            }
            if (strong == 4)
            {
                PluginLog.Debug("We have all the strong peaks we need! All unknowns are weak");
                Config.unknownD2Items.Clear();
            }
            else if (weak - Config.unknownD2Items.Count == 4)
            {
                PluginLog.Debug("We have all the weak we need! All unknowns are strong");
                foreach (var item in Config.unknownD2Items.Keys)
                {
                    Items[(int)item].peak = Cycle2Strong;
                }
                Config.unknownD2Items.Clear();
            }


            Config.Save();

        }

        if (CurrentDay < 6)
        {
            //Set reserved items
            Dictionary<Item, int> itemValues = new Dictionary<Item, int>();
            foreach (ItemInfo item in Items)
            {
                if (item.time == 4)
                    continue;
                int value = item.GetValueWithSupply(Supply.Sufficient);
                if (ValuePerHour)
                    value = value * 8 / item.time;
                itemValues.Add(item.item, value);
            }

            var orderedItems = itemValues.OrderByDescending(kvp => kvp.Value);
            var enumerator = orderedItems.GetEnumerator();

            ReservedItems.Clear();

            for (int i = 0; i < ItemsToReserve && enumerator.MoveNext(); i++)
            {
                ReservedItems.Add(enumerator.Current.Key);
            }
        }

        PluginLog.Debug("Reserving items {0} today.", String.Join(", ", ReservedItems));
        CheckEndDaySummaries();


        InitStep = 2;

    }
    private static void CheckEndDaySummaries()
    {
        TotalGross = 0;
        TotalNet = 0;
        for (int summary = 1; summary < Importer.endDays.Count && summary <= CurrentDay; summary++)
        {
            var prevDaySummary = Importer.endDays[summary];
            PluginLog.LogDebug("previous day summary: " + prevDaySummary);
            if (prevDaySummary.crafts != null && prevDaySummary.crafts.Count > 0)
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
                prevDaySummary.endingGross = gross;
                prevDaySummary.endingNet = net;
                prevDaySummary.valuesPerCraft = yesterdaySchedule.cowriesPerHour;
            }

            if (prevDaySummary.endingGross > 0)
            {
                TotalGross += prevDaySummary.endingGross;
                TotalNet += prevDaySummary.endingNet;
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

        if (Importer.endDays.Count > CurrentDay)
        {
            bool endOfWeek = CurrentDay > 2;
            bool endOfWeekValid = false;
            for (int i = CurrentDay + 1; i < Importer.endDays.Count; i++)
            {
                var futureDay = Importer.endDays[i];
                if (i == CurrentDay + 1 || endOfWeek)
                {
                    PluginLog.Debug("Future day summary: Day: {1}, Items: {0}", String.Join(',', futureDay.crafts), (i + 1));
                    SetDay(futureDay.crafts, i);
                    if (endOfWeek && futureDay.crafts.Count > 0)
                        endOfWeekValid = true;
                }
            }

            if (endOfWeek && !endOfWeekValid)
            {
                for (int i = CurrentDay + 1; i < Importer.endDays.Count; i++)
                {
                    RemoveSetDay(i);
                }
            }
        }
    }
    static public List<(int, SuggestedSchedules?)>? RunSolver(Dictionary<int, int> inventory)
    {
        if (InitStep != 2)
        {
            PluginLog.LogError("Trying to run solver before solver initiated");
            return null;
        }

        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        int dayToSolve = CurrentDay + 1;

        Inventory = inventory;
        List<(int, SuggestedSchedules?)> toReturn = new List<(int, SuggestedSchedules?)>();
        if (dayToSolve == 1)
        {
            toReturn.Add((0, null));

            SetDay(new List<Item>(), 0);
        }

        if(dayToSolve >= 4 && Importer.NeedCurrentPeaks())
            Importer.WriteCurrentPeaks(Week);
        
        if (!Importer.HasAllPeaksForward(dayToSolve)) //We don't know the whole week, so just solve the day in front of us
        {
            Dictionary<WorkshopSchedule, int> safeSchedules = GetSuggestedSchedules(dayToSolve, -1, null);

            var bestSched = GetBestSchedule(safeSchedules);

            if (!Rested || !Config.enforceRestDays)
                AddRestDayValue(safeSchedules, GetWorstFutureDay(bestSched, dayToSolve));


            toReturn.Add((dayToSolve, new SuggestedSchedules(safeSchedules)));
        }
        else if (dayToSolve < 7) //We know the rest of the week
        {
            if (dayToSolve == 4)
                toReturn.AddRange(GetLastThreeDays());
            else if (dayToSolve == 5)
                toReturn.AddRange(GetLastTwoDays());
            else if (Rested || !Config.enforceRestDays)
                toReturn.Add((dayToSolve, new SuggestedSchedules(GetSuggestedSchedules(dayToSolve, -1, null))));
            else
                toReturn.Add((dayToSolve, null));
        }

        PluginLog.LogInformation("Took {0} ms to calculate suggestions for day {1}. Suggestions length: {2}", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time, dayToSolve + 1, toReturn.Count);

        return toReturn;

    }

    public static void AddStubValue(int day, int groove, int value)
    {
        int prevGroove = GetEndingGrooveForDay(day - 1);
        PluginLog.Debug("Writing new end day summary with day {0}, endingGroove {1}, and gross {2}", day + 1, prevGroove + groove, value);
        Importer.WriteEndDay(day, prevGroove + groove, value, value, new List<Item>());
        Importer.endDays[day] = new EndDaySummary(new List<int>(), prevGroove + groove, value, value, new List<Item>());
        CheckEndDaySummaries();
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
        Dictionary<Material, int> mats = GetAllScheduledMats();

        if (mats.Count == 0)
            return null;

        var orderedDict = mats.OrderByDescending(mat => { RareMaterialHelper.GetMaterialValue(mat.Key, out int value); return value; });
        return orderedDict;
    }

    private static Dictionary<Material, int> GetAllScheduledMats(int exceptDay = -1)
    {
        int currentHour = GetCurrentHour();
        Dictionary<Material, int> mats = new Dictionary<Material, int>();
        if (Importer.endDays.Count > CurrentDay && Importer.endDays[CurrentDay] != null && Importer.endDays[CurrentDay].crafts != null && Importer.endDays[CurrentDay].crafts.Count > 0)
        {
            CycleSchedule today = new CycleSchedule(CurrentDay, 0);
            today.SetForAllWorkshops(Importer.endDays[CurrentDay].crafts);
            mats = today.GetMaterialsNeededAfterHour(currentHour);
        }

        foreach (var schedule in SchedulesPerDay)
        {
            if (schedule.Key > CurrentDay && schedule.Key != exceptDay)
                GetAllMatsForSchedule(schedule.Value.schedule.workshops[0], mats);
        }
        return mats;
    }

    public static void GetAllMatsForSchedule(WorkshopSchedule schedule, in Dictionary<Material, int> mats)
    {
        foreach (var item in schedule.GetItems())
        {
            foreach (var mat in Items[(int)item].materialsRequired)
            {
                if (mats.ContainsKey(mat.Key))
                    mats[mat.Key] += mat.Value * NumWorkshops;
                else
                    mats.Add(mat.Key, mat.Value * NumWorkshops);
            }
        }
    }

    
    public static List<(int, SuggestedSchedules?)> GetLastTwoDays()
    {
        Dictionary<Item, int>? reservedFor6 = null;
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
                dayRested = 6;
            reservedFor6 = schedule7.schedule.workshops[0].GetLimitedUses();
        }

        List<Dictionary<WorkshopSchedule, int>> initialSchedules = new List<Dictionary<WorkshopSchedule, int>>
        {
            GetSuggestedSchedules(5, startingGroove, reservedFor6, sevenSet?6:5),
            GetSuggestedSchedules(6, startingGroove)
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
        suggested.Add((5, new SuggestedSchedules(initialSchedules[0])));
        suggested.Add((6, new SuggestedSchedules(initialSchedules[1])));
        return suggested;
    }
    public static List<(int, SuggestedSchedules?)> GetLastThreeDays()
    {
        Dictionary<Item, int>? reservedFor6 = null;
        Dictionary<Item, int>? reservedFor5 = null;
        int startingGroove = GetEndingGrooveForDay(CurrentDay);
        bool fiveSet = false;
        bool sixSet = false;
        bool sixFinal = false;
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
            sixSet = true;
            List<Item> items6 = schedule6.schedule.workshops[0].GetItems();
            if (fiveSet)
            {
                SetDay(items6, 5); //Recalculate with 5's groove
                sixFinal = true;
            }
            if (schedule6.schedule.workshops[0].GetItems().Count == 0)
                dayRested = 5;

            reservedFor5 = schedule6.schedule.workshops[0].GetLimitedUses();
        }

        if (SchedulesPerDay.TryGetValue(6, out var schedule7))
        {
            sevenSet = true;
            List<Item> items7 = schedule7.schedule.workshops[0].GetItems();
            if (sixFinal)
            {
                SetDay(items7, 6); //Recalculate with 6's groove
            }
            if (schedule7.schedule.workshops[0].GetItems().Count == 0)
                dayRested = 6;
            reservedFor5 = schedule7.schedule.workshops[0].GetLimitedUses(reservedFor5);
            reservedFor6 = schedule7.schedule.workshops[0].GetLimitedUses(reservedFor6);
        }

        List<Dictionary<WorkshopSchedule, int>> initialSchedules = new List<Dictionary<WorkshopSchedule, int>>
        {

            GetSuggestedSchedules(4, startingGroove, reservedFor5, sevenSet&&sixSet?6:sixSet?5:4),
            GetSuggestedSchedules(5, startingGroove, reservedFor6, sevenSet?6:5),
            GetSuggestedSchedules(6, startingGroove)
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
        suggested.Add((4, new SuggestedSchedules(initialSchedules[0])));
        suggested.Add((5, new SuggestedSchedules(initialSchedules[1])));
        suggested.Add((6, new SuggestedSchedules(initialSchedules[2])));
        return suggested;
    }

    private static int GetWorstFutureDay(KeyValuePair<WorkshopSchedule, int> rec, int day)
    {
        int worstInFuture = -1;
        PluginLog.LogDebug("Comparing d" + (day + 1) + " (" + rec.Value + ") to worst-case future days");
        int currentGroove = GetEndingGrooveForDay(CurrentDay);
        Dictionary<Item, int> reservedSet = new Dictionary<Item, int>();
        foreach (Item item in rec.Key.GetItems())
        {
            if (!reservedSet.ContainsKey(item))
                reservedSet.Add(item, 0);
        }
        for (int d = day + 1; d < 7; d++)
        {
            KeyValuePair<WorkshopSchedule, int> solution;
            if (day == 3 && d == 4) //We have a lot of info about this specific pair so we might as well use it
                solution = GetD5EV();
            else
                solution = GetBestSchedule(d, currentGroove, reservedSet, d);

            if(solution.Key!=null)
            {

                PluginLog.LogDebug("Day " + (d + 1) + ", crafts: " + String.Join(", ", solution.Key.GetItems()) + " value: " + solution.Value);
                if (worstInFuture == -1)
                    worstInFuture = solution.Value;
                else
                    worstInFuture = Math.Min(worstInFuture, solution.Value);
                foreach (Item item in solution.Key.GetItems())
                {
                    if (!reservedSet.ContainsKey(item))
                        reservedSet.Add(item, 0);
                }
                    
            }

        }
            PluginLog.LogDebug("Worst future day: " + worstInFuture);

        return worstInFuture;
    }

    //Specifically for comparing D4 to D5
    public static KeyValuePair<WorkshopSchedule, int> GetD5EV()
    {
        int currentGroove = GetEndingGrooveForDay(CurrentDay);
        KeyValuePair<WorkshopSchedule, int> solution = GetBestSchedule(4, currentGroove);
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
            PluginLog.LogDebug("Getting ending groove from scheduled day " +(day+1)+": " + schedule.schedule.endingGroove);
            return schedule.schedule.endingGroove;
        }
        PluginLog.LogDebug("Can't find day in summaries or schedules. Returning 0");

        return 0;
    }

    private static void RemoveSetDay(int day)
    {
        if (SchedulesPerDay.TryGetValue(day, out var previousSchedule))
        {
            if(previousSchedule.value > 0)
            {
                TotalGross -= previousSchedule.value;
                TotalNet -= (previousSchedule.value - previousSchedule.schedule.GetMaterialCost());
            }
            SchedulesPerDay.Remove(day);
            foreach (var item in Items)
                item.SetCrafted(0, day);
        }
    }

    public static void SetDay(List<Item> crafts, int day)
    {
        if (day != 0)
            PluginLog.LogInformation("Day {0}, crafts: {1}", day+1, crafts);

        CycleSchedule schedule = new CycleSchedule(day, 0);
        schedule.SetForAllWorkshops(crafts);
        RemoveSetDay(day);
        
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
    private static Dictionary<WorkshopSchedule, int> GetSuggestedSchedules(int day, int startingGroove = -1, Dictionary<Item, int>? limitedUse = null, int allowUpToDay = -1)
    {
        if (startingGroove == -1)
            startingGroove = GetEndingGrooveForDay(day - 1);
        if (allowUpToDay == -1)
            allowUpToDay = day;

        var fourHour = new List<ItemInfo>();
        var eightHour = new List<ItemInfo>();
        var sixHour = new List<ItemInfo>();

        Dictionary<Material, int>? materials = null;
        if (Config.onlySuggestMaterialsOwned)
            materials = GetAllScheduledMats(day);

        foreach (ItemInfo item in Items)
        {
            List<ItemInfo>? bucket = null;

            if (item.time == 4 && item.rankUnlocked <= IslandRank && item.PeaksOnOrBeforeDay(allowUpToDay))
                bucket = fourHour;
            else if (item.time == 6 && item.rankUnlocked <= IslandRank && item.PeaksOnOrBeforeDay(allowUpToDay))
                bucket = sixHour;
            else if (item.time == 8 && item.rankUnlocked <= IslandRank && item.PeaksOnOrBeforeDay(allowUpToDay))
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
                AddToScheduleMap(new List<Item> { topItem.item, eightMatchEnum.Current.item, topItem.item }, day, safeSchedules, limitedUse, materials, startingGroove);
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
                        day, safeSchedules, limitedUse, materials, startingGroove);


                    if (!secondFourMatchEnum.Current.GetsEfficiencyBonus(firstFourMatchEnum.Current))
                        continue;

                    //4-4-8-8
                    foreach(var eightMatch in eightMatches)
                        AddToScheduleMap(new List<Item> { secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item, eightMatch.item },
                            day, safeSchedules, limitedUse, materials, startingGroove);

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
                                day, safeSchedules, limitedUse, materials, startingGroove);
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
                var sixSixMatchEnum = sixHour.GetEnumerator();
                while (sixSixMatchEnum.MoveNext())
                {
                    if (!sixSixMatchEnum.Current.GetsEfficiencyBonus(sixHourMatch))
                        continue;

                    //4-6-6-8
                    var fourSixMatchEnum = fourHour.GetEnumerator();
                    while (fourSixMatchEnum.MoveNext())
                    {
                        AddScheduleIfEfficient(fourSixMatchEnum.Current, sixSixMatchEnum.Current,
                        new List<Item> { fourSixMatchEnum.Current.item, sixSixMatchEnum.Current.item, sixHourMatch.item, topItem.item },
                        day, safeSchedules, limitedUse, materials, startingGroove);
                    }
                }

                var fourMatchEnum = fourHour.GetEnumerator();
                while (fourMatchEnum.MoveNext())
                {
                    if (!fourMatchEnum.Current.GetsEfficiencyBonus(sixHourMatch))
                        continue;

                    var other6MatchEnum = sixHour.GetEnumerator();
                    while (other6MatchEnum.MoveNext())
                    {
                        AddScheduleIfEfficient(other6MatchEnum.Current, topItem,
                        new List<Item> { fourMatchEnum.Current.item, sixHourMatch.item, topItem.item, other6MatchEnum.Current.item },
                        day, safeSchedules, limitedUse, materials, startingGroove);
                    }

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
                    AddToScheduleMap(new List<Item> { secondSix.item, topItem.item, firstSix.item, topItem.item },
                    day, safeSchedules, limitedUse, materials, startingGroove);
                }
            }

            
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
                        day, safeSchedules, limitedUse, materials, startingGroove);
                }

                var secondFourMatchEnum = fourHour.GetEnumerator();
                while (secondFourMatchEnum.MoveNext())
                {
                    if (!secondFourMatchEnum.Current.GetsEfficiencyBonus(firstFourMatchEnum.Current))
                        continue;

                    var thirdFourMatchEnum = fourHour.GetEnumerator();
                    while (thirdFourMatchEnum.MoveNext())
                    {
                        //4-4-6-4-6
                        AddScheduleIfEfficient(thirdFourMatchEnum.Current, topItem,
                        new List<Item> { secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item, thirdFourMatchEnum.Current.item, topItem.item },
                        day, safeSchedules, limitedUse, materials, startingGroove);
                        if (!secondFourMatchEnum.Current.GetsEfficiencyBonus(thirdFourMatchEnum.Current))
                            continue;
                        var fourthFourMatchEnum = fourHour.GetEnumerator();
                        while (fourthFourMatchEnum.MoveNext())
                        {
                            //4-4-4-4-6
                            AddScheduleIfEfficient(fourthFourMatchEnum.Current, thirdFourMatchEnum.Current,
                                new List<Item> { fourthFourMatchEnum.Current.item, thirdFourMatchEnum.Current.item, secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item },
                                day, safeSchedules, limitedUse, materials, startingGroove);
                        }
                    }
                }
            }
        }
        
        return safeSchedules;
    }

    private static KeyValuePair<WorkshopSchedule, int> GetBestSchedule(int day, int groove, Dictionary<Item, int>? limitedUse = null, int allowUpToDay = -1)
    {
        var suggested = GetSuggestedSchedules(day, groove, limitedUse, allowUpToDay);
        
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

    public static bool AddScheduleIfEfficient(ItemInfo newItem, ItemInfo origItem, List<Item> scheduledItems, int day, Dictionary<WorkshopSchedule, int> safeSchedules, Dictionary<Item, int>? limitedUse, Dictionary<Material, int>? materialsUsed, int startingGroove)
    {
        if (!newItem.GetsEfficiencyBonus(origItem))
            return false;


        AddToScheduleMap(scheduledItems, day, safeSchedules, limitedUse, materialsUsed, startingGroove);
        return true;
    }

    private static int AddToScheduleMap(List<Item> list, int day, Dictionary<WorkshopSchedule, int> safeSchedules, Dictionary<Item, int>? limitedUse, Dictionary<Material, int>? materialsUsed, int startingGroove)
    {
        WorkshopSchedule workshop = new WorkshopSchedule(list);
        if (workshop.UsesTooMany(limitedUse))
            return 0;

        if(materialsUsed!=null)
        {
            Dictionary<Material, int> totalMaterials = new Dictionary<Material, int>(materialsUsed);
            GetAllMatsForSchedule(workshop, totalMaterials);
            foreach(var mat in totalMaterials)
            {
                //PluginLog.Debug("Checking material {1}x {0}, in inventory: {2} ", mat.Key, mat.Value, Inventory.ContainsKey((int)mat.Key) ? Inventory[(int)mat.Key] : 0);
                if (RareMaterialHelper.GetMaterialValue(mat.Key, out _) && (!Inventory.ContainsKey((int)mat.Key) || Inventory[(int)mat.Key] < mat.Value))
                {
                    return 0;
                }
            }
        }

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
        if (day >= 6) //If we're day 7 (or later, somehow??), stop this foolishness
            return false;

        bool hasCurrentDay = false;

        for (int i = 0; i < Importer.observedSupplies.Count; i++)
        {
            if (Importer.observedSupplies[i].ContainsKey(day))
            {
                hasCurrentDay = true;
                ObservedSupply ob = Importer.observedSupplies[i][day];
                int observedHour = 0;
                if (Importer.observedSupplyHours.Count > day)
                    observedHour = Importer.observedSupplyHours[day];
                Items[i].AddObservedDay(ob, day, observedHour);
            }

            if (day < Importer.endDays.Count && Importer.endDays[day].NumCraftedCount() > i)
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


        bool needNewWeek = CurrentDay < 6 && Importer.NeedNewWeekData(Week);
        bool needNewData = CurrentDay < 6 && Importer.NeedNewTodayData(CurrentDay);

        if (!(needNewData || needNewWeek || needOverwrite))
            return true;


        PluginLog.LogInformation("Trying to write supply info starting with " + products[0] + ", last updated day " + (updatedDay + 1));

        if (updatedDay == CurrentDay && IsProductsValid(products))
        {
            PluginLog.LogDebug("Products are valid and updated today");
            if (needNewWeek || (CurrentDay == 0 && needOverwrite))
            {
                PluginLog.LogDebug("Writing week start info (names and popularity)");
                Importer.WriteWeekStart(products);
            }
            
            if(needNewData || needOverwrite)
            {
                PluginLog.LogDebug("Writing day supply info");
                Importer.WriteNewSupply(products, CurrentDay);
            }
            return true;
        }
        else if(Importer.HasAllPeaks())
            return true;

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
