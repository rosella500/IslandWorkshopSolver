using Dalamud.Hooking;
using Lumina.Data.Files;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.ConfigModule;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

namespace IslandWorkshopSolver.Solver;
using static Item;
using static ItemCategory;
using static RareMaterial;
using static PeakCycle;
public class Solver
{
    public static int WORKSHOP_BONUS = 120;
    public static int GROOVE_MAX = 35;

    public static List<ItemInfo> items;

    public static int totalGross;
    private static int totalNet;
    public static bool rested;

    public static bool verboseCalculatorLogging = false;
    public static bool verboseSolverLogging = false;
    public static bool verboseRestDayLogging = false;
    private static int alternatives = 0;
    public static int groovePerFullDay = 40;
    public static int groovePerPartDay = 15;
    private static int islandRank = 10;
    public static double materialWeight = 0.5;
    public static CSVImporter importer;
    public static int week = 5;
    static int initStep = 0;
    public static int currentDay = -1;
    private static Configuration config;
    private static Dictionary<int, (CycleSchedule schedule, int value)> schedulesPerDay = new Dictionary<int, (CycleSchedule schedule, int value)>();
    public static Dictionary<int, int> restOpportunityCost = new Dictionary<int, int>();

    public static void Init(Configuration newConfig)
    {
        config = newConfig;
        materialWeight = config.materialValue;
        WORKSHOP_BONUS = config.workshopBonus;
        GROOVE_MAX = config.maxGroove;
        islandRank = config.islandRank;
        verboseSolverLogging = config.verboseSolverLogging;
        verboseCalculatorLogging = config.verboseCalculatorLogging;
        verboseRestDayLogging = config.verboseRestDayLogging;

        if (initStep!=0)
            return;
        SupplyHelper.DefaultInit();
        PopularityHelper.DefaultInit();
        RareMaterialHelper.DefaultInit();
        initItems();
        week = getCurrentWeek();
        currentDay = getCurrentDay();
        config.day = currentDay;
        config.Save();
        try
        {
            importer = new CSVImporter(config.rootPath, week);
            initStep = 1;
        }
        catch(Exception e)
        {
            Dalamud.Chat.PrintError("Error importing file :" + e.Message + "\n" + e.StackTrace);
        }
    }

    public static void InitCSVs()
    {

    }

    public static void InitAfterWritingTodaysData()
    {
        if (initStep != 1)
            return;

        totalGross = 0;
        totalNet = 0;
        rested = false;

        int dayToSolve = currentDay + 1;

        setInitialFromCSV();
        for (int i = 1; i < dayToSolve; i++)
            setObservedFromCSV(i);

        for(int summary = 1; summary < importer.endDays.Count && summary <= currentDay; summary++)
        {
            var prevDaySummary = importer.endDays[summary];
            //Dalamud.Chat.Print("previous day summary: " + prevDaySummary);
            if (prevDaySummary.crafts != null)
            {
                var twoDaysAgo = importer.endDays[summary-1];
                CycleSchedule yesterdaySchedule = new CycleSchedule(summary, twoDaysAgo.endingGroove);
                yesterdaySchedule.setForAllWorkshops(prevDaySummary.crafts);
                int gross = yesterdaySchedule.getValue();

                if(prevDaySummary.endingGross == -1)
                {
                    int net = gross - yesterdaySchedule.getMaterialCost();
                    importer.writeEndDay(summary, prevDaySummary.endingGroove, gross, net, prevDaySummary.crafts);
                    totalGross += gross;
                    totalNet += net;
                }
                prevDaySummary.valuesPerCraft = yesterdaySchedule.cowriesPerHour;
            }
        }
        updateRestedStatus();
        initStep = 2;

    }
    static public List<(int,SuggestedSchedules?)>? RunSolver()
    {
        if (initStep !=2)
        {
            Dalamud.Chat.PrintError("Trying to run solver before solver initiated");
            return null;
        }
            
        //TODO: Figure out how to handle D2 because no one's going to craft things D1 to find out
        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        int dayToSolve = currentDay + 1;

        if (currentDay == 0 && config.unknownD2Items != null)
        {
            //Set each peak according to config
            foreach(var item in config.unknownD2Items)
            {
                items[(int)(item.Key)].peak = item.Value ? Cycle2Strong : Cycle2Weak;
            }
        }

        //Dalamud.Chat.Print("we made " + groove + " groove yesterday");


        List<(int, SuggestedSchedules?)> toReturn = new List<(int, SuggestedSchedules?)>();
        if (dayToSolve == 1)
        {
            toReturn.Add((0, null));

            setDay(new List<Item>(), 0);
        }

        if(dayToSolve < 4)
        {
            Dictionary<WorkshopSchedule, int> safeSchedules = getSuggestedSchedules(dayToSolve, null);

            //This is faster than just using LINQ, lol
            var bestSched = getBestSchedule(safeSchedules);

            if (!rested)
                addRestDayValue(safeSchedules,getWorstFutureDay(bestSched, dayToSolve));


            toReturn.Add((dayToSolve, new SuggestedSchedules(safeSchedules)));
        }
        else
        {
            if (importer.currentPeaks == null || importer.currentPeaks[0] == Unknown)
                importer.writeCurrentPeaks(week);

            if (dayToSolve == 4) //3 days to calculate
            {
                toReturn.AddRange(getLateDays());
            } 
            else if (dayToSolve == 5) //2 days to calculate
            {
                toReturn.AddRange(getLastTwoDays());
            }
            else if (dayToSolve == 6)
            {
                if (rested) //If we've rested, just get the best schedules for today
                    toReturn.Add((6, new SuggestedSchedules(getSuggestedSchedules(dayToSolve, null))));
                else //If we haven't rested, go to bed!!
                    toReturn.Add((6, null));
            }
        }
        //Technically speaking we can log in on D7 but there's nothing we can really do

        Dalamud.Chat.Print("Took " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time) + "ms.");

        return toReturn;

    }

    public static void addRestDayValue(Dictionary<WorkshopSchedule, int> safeSchedules, int restValue)
    {
        safeSchedules.Add(new WorkshopSchedule(new List<Item>()), restValue);
    }

    public static void addUnknownD2(Item item)
    {
        if (config.unknownD2Items == null)
            config.unknownD2Items = new Dictionary<Item, bool>();
        if (!config.unknownD2Items.ContainsKey(item))
            config.unknownD2Items.Add(item, false);
    }

    private static void addOrRest(KeyValuePair<WorkshopSchedule, int> rec, int day)
    {
        if (!rested && rec.Value <= getWorstFutureDay(rec, day))
        {
            printRestDayInfo(rec.Key.getItems(), day);
            rested = true;
            setDay(new List<Item>(), day);
        }
        else
        {

            setDay(rec.Key.getItems(), day);
        }
    }

    private static void printRestDayInfo(List<Item> rec, int day)
    {
        CycleSchedule restedDay = new CycleSchedule(day, getEndingGrooveForDay(day-1));
        restedDay.setForAllWorkshops(rec);
        Dalamud.Chat.Print("Think we're guaranteed to make more than " + restedDay.getValue() + " with " + String.Join(", ",rec) + ". Rest day " + (day + 1));
    }


    private static KeyValuePair<WorkshopSchedule, int> getBestSchedule(Dictionary<WorkshopSchedule, int> schedulesAvailable)
    {
        KeyValuePair<WorkshopSchedule, int> bestSched = schedulesAvailable.First();
        foreach (var sched in schedulesAvailable)
        {
            if (sched.Value > bestSched.Value) bestSched = sched;
        }
        return bestSched;
    }
    private static List<(int,SuggestedSchedules?)> getLateDays()
    {
        List<Dictionary<WorkshopSchedule, int>> initialSchedules = new List<Dictionary<WorkshopSchedule, int>> { getSuggestedSchedules(4, null),
            getSuggestedSchedules(5, null), getSuggestedSchedules(6, null)};
        List<KeyValuePair<WorkshopSchedule, int>> initialBests = new List<KeyValuePair<WorkshopSchedule, int>> { getBestSchedule(initialSchedules[0]),
            getBestSchedule(initialSchedules[1]), getBestSchedule(initialSchedules[2])};

        List<(int, SuggestedSchedules?)> suggestedSchedules = new List<(int, SuggestedSchedules?)>();

        //I'm just hardcoding this, This could almost certainly be improved


        int bestDay = -1;
        int bestDayValue = -1;
        int worstDay = -1;
        int worstDayValue = -1;


        for (int i = 0; i < 3; i++)
        {
            int value = initialBests[i].Value;
            if (bestDay == -1 || value > bestDayValue)
            {
                bestDay = i + 4;
                bestDayValue = value;
            }
            if (worstDay == -1 || value < worstDayValue)
            {
                worstDay = i + 4;
                worstDayValue = value;
            }
        }

        if (bestDay == 4) //Day 5 is best
        {
            //Temporarily adding day 5 so we recalculate later days with its supply changes
            setDay(initialBests[0].Key.getItems(), 4);
            Dictionary<WorkshopSchedule, int> recalced6;
            Dictionary<WorkshopSchedule, int> recalced7;
            KeyValuePair<WorkshopSchedule, int> best7;
            KeyValuePair<WorkshopSchedule, int> best6;
            if (worstDay == 5)
            {
                //Recalc 7 in case 5 used some of its 4hrs
                recalced7 = getSuggestedSchedules(6, null);
                best7 = getBestSchedule(recalced7);

                //Day 6 is worst, so recalculate it according to day 7
                recalced6 = getSuggestedSchedules(5, new HashSet<Item>(best7.Key.getItems()));
                best6 = getBestSchedule(recalced6);
            }
            else //6 is second best, just recalcing and adding
            {
                recalced6 = getSuggestedSchedules(5, null);
                best6 = getBestSchedule(recalced6);
                setDay(best6.Key.getItems(), 5);
                recalced7 = getSuggestedSchedules(6, null);
                best7 = getBestSchedule(recalced7);
            }
            if (!rested)
            {
                addRestDayValue(recalced6, best7.Value);
                addRestDayValue(initialSchedules[0], Math.Min(best6.Value, best7.Value));
                addRestDayValue(recalced7, best6.Value);
            }


            setDay(new List<Item>(), 4); //Set d5 as rest day to clear it?

            suggestedSchedules.Add((4, new SuggestedSchedules(initialSchedules[0])));
            suggestedSchedules.Add((5, new SuggestedSchedules(recalced6)));
            suggestedSchedules.Add((6, new SuggestedSchedules(recalced7)));
            return suggestedSchedules;
        }
        else if (bestDay == 6) //Day 7 is best
        {
            //Dalamud.Chat.Print("Day 7 is best");
            HashSet<Item> reserved7Items = new HashSet<Item>(initialBests[2].Key.getItems());
            //Recalculate them both in case it's better just reserving day 7 (this is dangerous for d5)
            Dictionary<WorkshopSchedule, int> recalced5 = getSuggestedSchedules(4, reserved7Items);
            Dictionary<WorkshopSchedule, int> recalced6 = getSuggestedSchedules(5, reserved7Items);
            KeyValuePair<WorkshopSchedule, int> best5 = getBestSchedule(recalced5);
            KeyValuePair<WorkshopSchedule, int> best6 = getBestSchedule(recalced6);

            if (best5.Value < best6.Value) //Day 6 is second best
            {
                HashSet<Item> reserved67Items = new HashSet<Item>(reserved7Items);
                reserved67Items.UnionWith(best6.Key.getItems());

                recalced5 = getSuggestedSchedules(4, reserved67Items); //Recalc d5 reserving d6. This makes up for the danger from before
                //Dalamud.Chat.Print("Recalced 5: "+String.Join(", ",recalcedCycle5Sched.Key)+" value: "+recalcedCycle5Sched.Value);
                //Dalamud.Chat.Print("Recalcing 5 based on 6. Is it better?");
                best5 = getBestSchedule(recalced5);

                if (rested && best5.Value + best6.Value < initialBests[0].Value + initialBests[1].Value) //If we're using all 3 days, allow D5 some opinion on what D6 should be
                {
                    recalced5 = initialSchedules[0];
                    setDay(initialBests[0].Key.getItems(), 4);
                    //Dalamud.Chat.Print("Recalcing 6 AGAIN just in case 5 changed it, still only forbidding things used day 7");
                    recalced6 = getSuggestedSchedules(5, reserved7Items);
                }
            }
            else //Day 5 is second best 
            {
                setDay(best5.Key.getItems(), 4);
                //Recalc 6 to see if it changes because we committed to 5 
                recalced6 = getSuggestedSchedules(5, reserved7Items);
                best6 = getBestSchedule(recalced6);
                int recalcedTotal = best5.Value + best6.Value;
                if (rested && recalcedTotal < initialBests[0].Value + initialBests[1].Value) //If we're using all 3 days, allow D6 some opinion on what D5 should be
                {
                    setDay(initialBests[0].Key.getItems(), 4);
                    //Dalamud.Chat.Print("Recalcing 6 AGAIN just in case 5 changed it, still only forbidding things used day 7");
                    recalced6 = getSuggestedSchedules(5, reserved7Items);
                    best6 = getBestSchedule(recalced6);

                    if(initialBests[0].Value + best6.Value < recalcedTotal) //Wait, nevermind, it only looked better because we didn't commit to D5, change it back
                    {
                        setDay(best5.Key.getItems(), 4);
                        //Recalc 6 to see if it changes because we committed to 5 
                        recalced6 = getSuggestedSchedules(5, reserved7Items);
                        best6 = getBestSchedule(recalced6);
                    }
                    else
                    {
                        recalced5 = initialSchedules[0];
                        best5 = initialBests[0];
                    }
                }
            }

            if(!rested)
            {
                addRestDayValue(initialSchedules[2], Math.Min(best5.Value, best6.Value));
                addRestDayValue(recalced6, best5.Value);
                addRestDayValue(recalced5, best6.Value);
            }

            setDay(new List<Item>(), 4);
            suggestedSchedules.Add((4, new SuggestedSchedules(recalced5)));
            suggestedSchedules.Add((5, new SuggestedSchedules(recalced6)));
            suggestedSchedules.Add((6, new SuggestedSchedules(initialSchedules[2])));
            return suggestedSchedules;

        }
        else //Best day is Day 6
        {

            //Dalamud.Chat.Print("Day 6 is best");
            setDay(initialBests[1].Key.getItems(), 5);

            Dictionary<WorkshopSchedule, int> recalced7;
            KeyValuePair<WorkshopSchedule, int> best7;
            Dictionary<WorkshopSchedule, int> recalced5;
            KeyValuePair<WorkshopSchedule, int> best5;
            var reservedD6 = new HashSet<Item>(initialBests[1].Key.getItems());

            //Day 5 is the worst, so let's figure out day 7 first
            if (worstDay == 4)
            {
                //Just recalc 7 now that we've locked in 6
                recalced7 = getSuggestedSchedules(6, null);
                best7 = getBestSchedule(recalced7);

                //D5 can use anything other than what D6 and 7 have used
                reservedD6.UnionWith(best7.Key.getItems());
                recalced5 = getSuggestedSchedules(4, reservedD6);
                best5 = getBestSchedule(recalced5);
            }
            else
            {
                //Let day 5 do whatever it wants as long as it doesn't use D6's stuff
                recalced5 = getSuggestedSchedules(4, reservedD6);
                best5 = getBestSchedule(recalced5);
                setDay(best5.Key.getItems(), 4);

                //See what D7's like now that we've committed 5 and 6
                recalced7 = getSuggestedSchedules(6, null);
                best7 = getBestSchedule(recalced7);
            }

            if (!rested)
            {
                addRestDayValue(initialSchedules[1], Math.Min(best5.Value, best7.Value));
                addRestDayValue(recalced7, best5.Value);
                addRestDayValue(recalced5, best7.Value);
            }

            setDay(new List<Item>(), 4);
            setDay(new List<Item>(), 5);
            suggestedSchedules.Add((4, new SuggestedSchedules(recalced5)));
            suggestedSchedules.Add((5, new SuggestedSchedules(initialSchedules[1])));
            suggestedSchedules.Add((6, new SuggestedSchedules(recalced7)));
        }
        return suggestedSchedules;
    }

    private static List<(int, SuggestedSchedules?)> getLastTwoDays()
    {
        List<(int, SuggestedSchedules?)> suggestedSchedules = new List<(int, SuggestedSchedules?)>();
        var orig6 = getSuggestedSchedules(5, null);
        var bestOrig6 = getBestSchedule(orig6);
        var orig7 = getSuggestedSchedules(6, null);
        var bestOrig7 = getBestSchedule(orig7);


        setDay(bestOrig6.Key.getItems(), 5);
        var recalced7 = getSuggestedSchedules(6, null);
        var bestNew7 = getBestSchedule(recalced7);
        var recalced6 = getSuggestedSchedules(5, new HashSet<Item>(bestOrig7.Key.getItems()));
        var bestNew6 = getBestSchedule(recalced6);

        Dictionary<WorkshopSchedule, int> best7Scheds;
        KeyValuePair<WorkshopSchedule, int> best7;
        Dictionary<WorkshopSchedule, int> best6Scheds;
        KeyValuePair<WorkshopSchedule, int> best6;

        if (bestNew6.Value + bestOrig7.Value > bestOrig6.Value + bestNew7.Value) //Better to let 6 use 7's leftovers
        {
            best7Scheds = orig7;
            best7 = bestOrig7;
            best6Scheds = recalced6;
            best6 = bestNew6;
        }
        else //Better to calc 6 and then 7
        {
            best6Scheds = orig6;
            best6 = bestOrig6;
            best7Scheds = recalced7;
            best7 = bestNew7;
        }

        if(!rested)
        {
            addRestDayValue(best7Scheds, best6.Value);
            addRestDayValue(best6Scheds, best7.Value);
        }
        setDay(new List<Item>(), 5);
        suggestedSchedules.Add((5, new SuggestedSchedules(best6Scheds)));
        suggestedSchedules.Add((6, new SuggestedSchedules(best7Scheds)));
        return suggestedSchedules;
    }

    private static int getWorstFutureDay(KeyValuePair<WorkshopSchedule, int> rec, int day)
    {
        int worstInFuture = 99999;
        if (verboseRestDayLogging)
            Dalamud.Chat.Print("Comparing d" + (day + 1) + " (" + rec.Value + ") to worst-case future days");
        HashSet<Item> reservedSet = new HashSet<Item>(rec.Key.getItems());
        for (int d = day + 1; d < 7; d++)
        {
            KeyValuePair<WorkshopSchedule, int> solution;
            if (day == 3 && d == 4) //We have a lot of info about this specific pair so we might as well use it
                solution = getD5EV();
            else
                solution = getBestSchedule(d, reservedSet, false);
            if (verboseRestDayLogging)
                Dalamud.Chat.Print("Day " + (d + 1) + ", crafts: " + String.Join(", ", solution.Key.getItems()) + " value: " + solution.Value);
            worstInFuture = Math.Min(worstInFuture, solution.Value);
            reservedSet.UnionWith(solution.Key.getItems());
        }
        if (verboseRestDayLogging)
            Dalamud.Chat.Print("Worst future day: " + worstInFuture);

        return worstInFuture;
    }

    //Specifically for comparing D4 to D5
    public static KeyValuePair<WorkshopSchedule, int> getD5EV()
    {
        KeyValuePair<WorkshopSchedule, int> solution = getBestSchedule(4, null);
        if (verboseSolverLogging)
            Dalamud.Chat.Print("Testing against D5 solution " + solution.Key.getItems());
        List<ItemInfo> c5Peaks = new List<ItemInfo>();
        foreach (Item item in solution.Key.getItems())
            if (items[(int)item].peak == Cycle5 && !c5Peaks.Contains(items[(int)item]))
                c5Peaks.Add(items[(int)item]);
        int sum = solution.Value;
        int permutations = (int)Math.Pow(2, c5Peaks.Count);
        if (verboseSolverLogging)
            Dalamud.Chat.Print("C5 peaks: " + c5Peaks.Count + ", permutations: " + permutations);

        for (int p = 1; p < permutations; p++)
        {
            for (int i = 0; i < c5Peaks.Count; i++)
            {
                bool strong = ((p) & (1 << i)) != 0; //I can't believe I'm using a bitwise and
                if (verboseSolverLogging)
                    Dalamud.Chat.Print("Checking permutation " + p + " for item " + c5Peaks[i].item + " " + (strong ? "strong" : "weak"));
                if (strong)
                    c5Peaks[i].peak = Cycle5Strong;
                else
                    c5Peaks[i].peak = Cycle5Weak;
            }

            int toAdd = solution.Key.getValueWithGrooveEstimate(4, getEndingGrooveForDay(currentDay));
            if (verboseSolverLogging)
                Dalamud.Chat.Print("Permutation " + p + " has value " + toAdd);
            sum += toAdd;
        }

        if (verboseSolverLogging)
            Dalamud.Chat.Print("Sum: " + sum + " average: " + sum / permutations);
        sum /= permutations;
        KeyValuePair<WorkshopSchedule, int> newSolution = new KeyValuePair<WorkshopSchedule, int>(solution.Key, sum);


        foreach (ItemInfo item in c5Peaks)
        {
            item.peak = Cycle5; //Set back to normal
        }
        return newSolution;
    }

    public static void setDay(List<Item> crafts, int day)
    {
        if (day != 0)
            Dalamud.Chat.Print("Day " + (day + 1) + ", crafts: " + String.Join(", ", crafts));

        setDay(crafts, crafts, crafts, day);
    }

    private static int getEndingGrooveForDay(int day)
    {
        if (importer.endDays.Count > day && day >=0)
            return importer.endDays[day].endingGroove;
        else if(schedulesPerDay.TryGetValue(day, out var schedule))
            return schedule.schedule.endingGroove;
        return 0;
    }

    public static void setDay(List<Item> crafts0, List<Item> crafts1, List<Item> crafts2, int day)
    {
        int groove = getEndingGrooveForDay(day - 1);
        CycleSchedule schedule = new CycleSchedule(day, groove);
        schedule.setWorkshop(0, crafts0);
        schedule.setWorkshop(1, crafts1);
        schedule.setWorkshop(2, crafts2);

        if(schedulesPerDay.TryGetValue(day, out var previousSchedule))
        {
            totalGross -= previousSchedule.value;
            totalNet -= (previousSchedule.value - previousSchedule.schedule.getMaterialCost());
            schedulesPerDay.Remove(day);
        }

        int gross = schedule.getValue();
        totalGross += gross;

        int net = gross - schedule.getMaterialCost();
        totalNet += net;
        int startingGroove = groove;
        groove = schedule.endingGroove;

        schedule.startingGroove = 0;
        bool oldVerbose = verboseCalculatorLogging;
        verboseCalculatorLogging = false;
        if(day != 0)
            Dalamud.Chat.Print("day " + (day + 1) + " total, 0 groove: " + schedule.getValue() + ". Starting groove " + startingGroove + ": " + gross + ", net " + net + ".");
        verboseCalculatorLogging = oldVerbose;
        schedule.startingGroove = startingGroove;

        foreach(var kvp in schedule.numCrafted)
        {
            items[(int)kvp.Key].setCrafted(kvp.Value, day);
        }
        schedulesPerDay.Add(day, (schedule, gross));

        if (schedule.hasAnyUnsurePeaks())
            importer.writeEndDay(day, groove, -1, -1, crafts0);
        else
            importer.writeEndDay(day, groove, gross, net, crafts0);

        updateRestedStatus();
    }

    public static void updateRestedStatus()
    {
        rested = false;
        foreach(var schedule in schedulesPerDay)
        {
            if (schedule.Key != 0 && schedule.Value.value == 0)
                rested = true;
        }
        for(int i=1; i<importer.endDays.Count; i++)
        {
            if (importer.endDays[i].endingGross == 0)
            {
                rested = true;
            }
        }
    }

    private static Dictionary<WorkshopSchedule, int> getSuggestedSchedules(int day, HashSet<Item>? reservedForLater, bool allowAllOthers = true)
    {
        var fourHour = new List<ItemInfo>();
        var eightHour = new List<ItemInfo>();
        var sixHour = new List<ItemInfo>();

        if (reservedForLater == null)
            allowAllOthers = false;

        foreach (ItemInfo item in items)
        {
            List<ItemInfo>? bucket = null;

            if (reservedForLater != null && reservedForLater.Contains(item.item))
                continue;

            if (item.time == 4 && item.rankUnlocked <= islandRank && (allowAllOthers || item.peaksOnOrBeforeDay(day, true)))
                bucket = fourHour;
            else if (item.time == 6 && item.rankUnlocked <= islandRank && (allowAllOthers || item.peaksOnOrBeforeDay(day, false)))
                bucket = sixHour;
            else if (item.time == 8 && item.rankUnlocked <= islandRank && (allowAllOthers || item.peaksOnOrBeforeDay(day, false)))
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
            if (verboseSolverLogging)
                Dalamud.Chat.PrintError("Building schedule around : " + topItem.item + ", peak: " + topItem.peak);


            //8-8-8
            var eightMatchEnum = eightHour.GetEnumerator();
            while (eightMatchEnum.MoveNext())
            {
                addScheduleIfEfficient(eightMatchEnum.Current, topItem,
                    new List<Item> { topItem.item, eightMatchEnum.Current.item, topItem.item }, day, safeSchedules);
            }

            //4-8-4-8 and 4-4-4-4-8
            var firstFourMatchEnum = fourHour.GetEnumerator();
            while (firstFourMatchEnum.MoveNext())
            {
                if (!firstFourMatchEnum.Current.getsEfficiencyBonus(topItem))
                    continue;

                if (verboseSolverLogging)
                    Dalamud.Chat.Print("Found 4hr match, matching with " + firstFourMatchEnum.Current.item);

                var secondFourMatchEnum = fourHour.GetEnumerator();
                while (secondFourMatchEnum.MoveNext())
                {
                    if (verboseSolverLogging)
                        Dalamud.Chat.Print("Checking potential 4hr match: " + secondFourMatchEnum.Current.item);
                    addScheduleIfEfficient(secondFourMatchEnum.Current, topItem,
                        new List<Item> { firstFourMatchEnum.Current.item, topItem.item, secondFourMatchEnum.Current.item, topItem.item },
                        day, safeSchedules);


                    if (!secondFourMatchEnum.Current.getsEfficiencyBonus(firstFourMatchEnum.Current))
                        continue;

                    var thirdFourMatchEnum = fourHour.GetEnumerator();
                    while (thirdFourMatchEnum.MoveNext())
                    {
                        if (!secondFourMatchEnum.Current.getsEfficiencyBonus(thirdFourMatchEnum.Current))
                            continue;


                        var fourthFourMatchEnum = fourHour.GetEnumerator();
                        while (fourthFourMatchEnum.MoveNext())
                        {
                            addScheduleIfEfficient(fourthFourMatchEnum.Current, thirdFourMatchEnum.Current,
                                new List<Item> { fourthFourMatchEnum.Current.item, thirdFourMatchEnum.Current.item, secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item },
                                day, safeSchedules);
                        }
                    }
                }
            }

            //4-6-8-6
            var sixMatchEnum = sixHour.GetEnumerator();
            while (sixMatchEnum.MoveNext())
            {
                var sixHourMatch = sixMatchEnum.Current;
                if (!sixHourMatch.getsEfficiencyBonus(topItem))
                    continue;
                var fourMatchEnum = fourHour.GetEnumerator();
                while (fourMatchEnum.MoveNext())
                {
                    addScheduleIfEfficient(fourMatchEnum.Current, sixHourMatch,
                        new List<Item> { fourMatchEnum.Current.item, sixHourMatch.item, topItem.item, sixHourMatch.item },
                        day, safeSchedules);
                }
            }
        }

        //Find schedules based on 6-hour crafts
        var sixEnum = sixHour.GetEnumerator();
        while (sixEnum.MoveNext())
        {
            var topItem = sixEnum.Current;

            if (verboseSolverLogging)
                Dalamud.Chat.PrintError("Building schedule around : " + topItem.item);


            //6-6-6-6
            HashSet<ItemInfo> sixMatches = new HashSet<ItemInfo>();
            var sixMatchEnum = sixHour.GetEnumerator();
            while (sixMatchEnum.MoveNext())
            {
                if (!sixMatchEnum.Current.getsEfficiencyBonus(topItem))
                    continue;
                sixMatches.Add(sixMatchEnum.Current);
            }
            foreach (ItemInfo firstSix in sixMatches)
            {
                foreach (ItemInfo secondSix in sixMatches)
                {
                    if (verboseSolverLogging)
                        Dalamud.Chat.Print("Adding 6-6-6-6 schedule made out of helpers " + firstSix.item + ", " + secondSix.item + ", and top item: " + topItem.item);
                    addToScheduleMap(new List<Item> { secondSix.item, topItem.item, firstSix.item, topItem.item },
                    day, safeSchedules);
                }
            }

            //4-4-4-4-6 and 4-4-6-4-6
            var firstFourMatchEnum = fourHour.GetEnumerator();
            while (firstFourMatchEnum.MoveNext())
            {
                if (!firstFourMatchEnum.Current.getsEfficiencyBonus(topItem))
                    continue;


                var sixFourMatchEnum = sixHour.GetEnumerator();
                //4-6-6-6
                while (sixFourMatchEnum.MoveNext())
                {
                    addToScheduleMap(new List<Item> { firstFourMatchEnum.Current.item, topItem.item, sixFourMatchEnum.Current.item, topItem.item },
                        day, safeSchedules);
                }

                var secondFourMatchEnum = fourHour.GetEnumerator();
                while (secondFourMatchEnum.MoveNext())
                {
                    if (!secondFourMatchEnum.Current.getsEfficiencyBonus(firstFourMatchEnum.Current))
                        continue;

                    addScheduleIfEfficient(secondFourMatchEnum.Current, topItem,
                        new List<Item> { firstFourMatchEnum.Current.item, secondFourMatchEnum.Current.item, topItem.item, firstFourMatchEnum.Current.item, topItem.item },
                        day, safeSchedules);
                    addToScheduleMap(new List<Item> { secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item, firstFourMatchEnum.Current.item, topItem.item },
                        day, safeSchedules);

                    var thirdFourMatchEnum = fourHour.GetEnumerator();
                    while (thirdFourMatchEnum.MoveNext())
                    {
                        if (!secondFourMatchEnum.Current.getsEfficiencyBonus(thirdFourMatchEnum.Current))
                            continue;
                        var fourthFourMatchEnum = fourHour.GetEnumerator();
                        while (fourthFourMatchEnum.MoveNext())
                        {
                            addScheduleIfEfficient(fourthFourMatchEnum.Current, thirdFourMatchEnum.Current,
                                new List<Item> { fourthFourMatchEnum.Current.item, thirdFourMatchEnum.Current.item, secondFourMatchEnum.Current.item, firstFourMatchEnum.Current.item, topItem.item },
                                day, safeSchedules);
                        }
                    }
                }
            }
        }
        
        return safeSchedules;
    }

    private static KeyValuePair<WorkshopSchedule, int> getBestSchedule(int day, HashSet<Item>? reservedForLater, bool allowAllOthers = true)
    {
        var suggested = new SuggestedSchedules(getSuggestedSchedules(day, reservedForLater, allowAllOthers));
        var scheduleEnum = suggested.orderedSuggestions.GetEnumerator();
        scheduleEnum.MoveNext();
        var bestSchedule = scheduleEnum.Current;


        if (alternatives > 0)
        {
            Dalamud.Chat.Print("Best rec: " + String.Join(", ", bestSchedule.Key) + ": " + bestSchedule.Value);
            int count = 0;
            for (int c = 0; c < alternatives && scheduleEnum.MoveNext(); c++)
            {
                var alt = scheduleEnum.Current;
                Dalamud.Chat.Print("Alternative rec: " + String.Join(", ", alt.Key) + ": " + alt.Value);
                count++;
            }
        }

        return bestSchedule;//new KeyValuePair<WorkshopSchedule, int>(new WorkshopSchedule(bestSchedule.Key), bestSchedule.Value);
    }

    public static bool addScheduleIfEfficient(ItemInfo newItem, ItemInfo origItem, List<Item> scheduledItems, int day, Dictionary<WorkshopSchedule, int> safeSchedules)
    {
        if (!newItem.getsEfficiencyBonus(origItem))
            return false;


        addToScheduleMap(scheduledItems, day, safeSchedules);
        return true;
    }

    private static int addToScheduleMap(List<Item> list, int day, Dictionary<WorkshopSchedule, int> safeSchedules)
    {
        WorkshopSchedule workshop = new WorkshopSchedule(list);

        int value = workshop.getValueWithGrooveEstimate(day, getEndingGrooveForDay(day - 1));
        //Only add if we don't already have one with this schedule or ours is better
        if(safeSchedules.TryGetValue(workshop, out int oldValue))
        {
            if (verboseSolverLogging)
                Dalamud.Chat.Print("Found workshop in safe schedules with rare mats: " + String.Join(", ", workshop.rareMaterialsRequired));
        }
        else
        {
            if (verboseSolverLogging)
                Dalamud.Chat.Print("Can't find workshop schedule out of "+safeSchedules.Count+" with rare mats: " + String.Join(", ", workshop.rareMaterialsRequired));
            oldValue = -1;
        }

        if (oldValue < value)
        {
            if (verboseSolverLogging && oldValue != -1)
                Dalamud.Chat.Print("Replacing schedule with mats " + String.Join(", ",workshop.rareMaterialsRequired) + " with " + String.Join(", ",list) + " because " + value + " is higher than " + oldValue);
            safeSchedules.Remove(workshop); //It doesn't seem to update the key when updating the value, so we delete the key first
            safeSchedules.Add(workshop, value);
        }
        else
        {
            if (verboseSolverLogging)
                Dalamud.Chat.Print("Not replacing schedule with mats " + String.Join(", ",workshop.rareMaterialsRequired) + " with " + String.Join(", ",list) + " because " + value + " is lower than " + oldValue);

                value = 0;
        }

        return value;

    }

    private static void setInitialFromCSV()
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i].setInitialData(importer.currentPopularity[i], importer.lastWeekPeaks[i], importer.observedSupplies[i][0]);
        }
    }

    private static void setObservedFromCSV(int day)
    {
        bool hasDaySummary = day < importer.endDays.Count;

        for (int i = 0; i < items.Count; i++)
        {
            if (day < importer.observedSupplies[i].Count)
            {
                ObservedSupply ob = importer.observedSupplies[i][day];
                items[i].addObservedDay(ob, day);
            }
            if (hasDaySummary && importer.endDays[day].craftedItems() > i)
                items[i].setCrafted(importer.endDays[day].getCrafted(i), day);
        }
        
        if(hasDaySummary && importer.endDays[day].endingGross > -1)
        {
            //Dalamud.Chat.Print("Adding totals from day " + day + ": " + importer.endDays[day]);
            totalGross += importer.endDays[day].endingGross;
            totalNet += importer.endDays[day].endingNet;
        }
    }

    public static void initItems()
    {
        items = new List<ItemInfo>();
        items.Add(new ItemInfo(Potion, Concoctions, Invalid, 28, 4, 1, null));
        items.Add(new ItemInfo(Firesand, Concoctions, UnburiedTreasures, 28, 4, 1, null));
        items.Add(new ItemInfo(WoodenChair, Furnishings, Woodworks, 42, 6, 1, null));
        items.Add(new ItemInfo(GrilledClam, Foodstuffs, MarineMerchandise, 28, 4, 1, null));
        items.Add(new ItemInfo(Necklace, Accessories, Woodworks, 28, 4, 1, null));
        items.Add(new ItemInfo(CoralRing, Accessories, MarineMerchandise, 42, 6, 1, null));
        items.Add(new ItemInfo(Barbut, Attire, Metalworks, 42, 6, 1, null));
        items.Add(new ItemInfo(Macuahuitl, Arms, Woodworks, 42, 6, 1, null));
        items.Add(new ItemInfo(Sauerkraut, PreservedFood, Invalid, 40, 4, 1, new Dictionary<RareMaterial, int>() { { Cabbage, 1 } }));
        items.Add(new ItemInfo(BakedPumpkin, Foodstuffs, Invalid, 40, 4, 1, new Dictionary<RareMaterial, int>() { { Pumpkin, 1 } }));
        items.Add(new ItemInfo(Tunic, Attire, Textiles, 72, 6, 1, new Dictionary<RareMaterial, int>() { { Fleece, 2 } }));
        items.Add(new ItemInfo(CulinaryKnife, Sundries, CreatureCreations, 44, 4, 1, new Dictionary<RareMaterial, int>() { { Claw, 1 } }));
        items.Add(new ItemInfo(Brush, Sundries, Woodworks, 44, 4, 1, new Dictionary<RareMaterial, int>() { { Fur, 1 } }));
        items.Add(new ItemInfo(BoiledEgg, Foodstuffs, CreatureCreations, 44, 4, 1, new Dictionary<RareMaterial, int>() { { Egg, 1 } }));
        items.Add(new ItemInfo(Hora, Arms, CreatureCreations, 72, 6, 1, new Dictionary<RareMaterial, int>() { { Carapace, 2 } }));
        items.Add(new ItemInfo(Earrings, Accessories, CreatureCreations, 44, 4, 1, new Dictionary<RareMaterial, int>() { { Fang, 1 } }));
        items.Add(new ItemInfo(Butter, Ingredients, CreatureCreations, 44, 4, 1, new Dictionary<RareMaterial, int>() { { Milk, 1 } }));
        items.Add(new ItemInfo(BrickCounter, Furnishings, UnburiedTreasures, 48, 6, 5, null));
        items.Add(new ItemInfo(BronzeSheep, Furnishings, Metalworks, 64, 8, 5, null));
        items.Add(new ItemInfo(GrowthFormula, Concoctions, Invalid, 136, 8, 5, new Dictionary<RareMaterial, int>() { { Alyssum, 2 } }));
        items.Add(new ItemInfo(GarnetRapier, Arms, UnburiedTreasures, 136, 8, 5, new Dictionary<RareMaterial, int>() { { Garnet, 2 } }));
        items.Add(new ItemInfo(SpruceRoundShield, Attire, Woodworks, 136, 8, 5, new Dictionary<RareMaterial, int>() { { Spruce, 2 } }));
        items.Add(new ItemInfo(SharkOil, Sundries, MarineMerchandise, 136, 8, 5, new Dictionary<RareMaterial, int>() { { Shark, 2 } }));
        items.Add(new ItemInfo(SilverEarCuffs, Accessories, Metalworks, 136, 8, 5, new Dictionary<RareMaterial, int>() { { Silver, 2 } }));
        items.Add(new ItemInfo(SweetPopoto, Confections, Invalid, 72, 6, 5, new Dictionary<RareMaterial, int>() { { Popoto, 2 }, { Milk, 1 } }));
        items.Add(new ItemInfo(ParsnipSalad, Foodstuffs, Invalid, 48, 4, 5, new Dictionary<RareMaterial, int>() { { Parsnip, 2 } }));
        items.Add(new ItemInfo(Caramels, Confections, Invalid, 81, 6, 6, new Dictionary<RareMaterial, int>() { { Milk, 2 } }));
        items.Add(new ItemInfo(Ribbon, Accessories, Textiles, 54, 6, 6, null));
        items.Add(new ItemInfo(Rope, Sundries, Textiles, 36, 4, 6, null));
        items.Add(new ItemInfo(CavaliersHat, Attire, Textiles, 81, 6, 6, new Dictionary<RareMaterial, int>() { { Feather, 2 } }));
        items.Add(new ItemInfo(Item.Horn, Sundries, CreatureCreations, 81, 6, 6, new Dictionary<RareMaterial, int>() { { RareMaterial.Horn, 2 } }));
        items.Add(new ItemInfo(SaltCod, PreservedFood, MarineMerchandise, 54, 6, 7, null));
        items.Add(new ItemInfo(SquidInk, Ingredients, MarineMerchandise, 36, 4, 7, null));
        items.Add(new ItemInfo(EssentialDraught, Concoctions, MarineMerchandise, 54, 6, 7, null));
        items.Add(new ItemInfo(Jam, Ingredients, Invalid, 78, 6, 7, new Dictionary<RareMaterial, int>() { { Isleberry, 3 } }));
        items.Add(new ItemInfo(TomatoRelish, Ingredients, Invalid, 52, 4, 7, new Dictionary<RareMaterial, int>() { { Tomato, 2 } }));
        items.Add(new ItemInfo(OnionSoup, Foodstuffs, Invalid, 78, 6, 7, new Dictionary<RareMaterial, int>() { { Onion, 3 } }));
        items.Add(new ItemInfo(Pie, Confections, MarineMerchandise, 78, 6, 7, new Dictionary<RareMaterial, int>() { { Wheat, 3 } }));
        items.Add(new ItemInfo(CornFlakes, PreservedFood, Invalid, 52, 4, 7, new Dictionary<RareMaterial, int>() { { Corn, 2 } }));
        items.Add(new ItemInfo(PickledRadish, PreservedFood, Invalid, 104, 8, 7, new Dictionary<RareMaterial, int>() { { Radish, 4 } }));
        items.Add(new ItemInfo(IronAxe, Arms, Metalworks, 72, 8, 8, null));
        items.Add(new ItemInfo(QuartzRing, Accessories, UnburiedTreasures, 72, 8, 8, null));
        items.Add(new ItemInfo(PorcelainVase, Sundries, UnburiedTreasures, 72, 8, 8, null));
        items.Add(new ItemInfo(VegetableJuice, Concoctions, Invalid, 78, 6, 8, new Dictionary<RareMaterial, int>() { { Cabbage, 3 } }));
        items.Add(new ItemInfo(PumpkinPudding, Confections, Invalid, 78, 6, 8, new Dictionary<RareMaterial, int>() { { Pumpkin, 3 }, { Egg, 1 }, { Milk, 1 } }));
        items.Add(new ItemInfo(SheepfluffRug, Furnishings, CreatureCreations, 90, 6, 8, new Dictionary<RareMaterial, int>() { { Fleece, 3 } }));
        items.Add(new ItemInfo(GardenScythe, Sundries, Metalworks, 90, 6, 9, new Dictionary<RareMaterial, int>() { { Claw, 3 } }));
        items.Add(new ItemInfo(Bed, Furnishings, Textiles, 120, 8, 9, new Dictionary<RareMaterial, int>() { { Fur, 4 } }));
        items.Add(new ItemInfo(ScaleFingers, Attire, CreatureCreations, 120, 8, 9, new Dictionary<RareMaterial, int>() { { Carapace, 4 } }));
        items.Add(new ItemInfo(Crook, Arms, Woodworks, 120, 8, 9, new Dictionary<RareMaterial, int>() { { Fang, 4 } }));
    }

    public static int getCurrentWeek()
    {
        //August 23 2022
        DateTime startOfIS = new DateTime(2022, 8, 23, 4, 0, 0);

        DateTime current = DateTime.Now;

        TimeSpan timeSinceStart = (current - startOfIS);
        int week = timeSinceStart.Days / 7 + 1;
        int day = timeSinceStart.Days % 7;

        //Dalamud.Chat.Print("Current week: " + week + " day: " + day);

        return week;
    }

    public static int getCurrentDay()
    {
        DateTime startOfIS = new DateTime(2022, 8, 23, 4, 0, 0);
        DateTime current = DateTime.Now;
        TimeSpan timeSinceStart = (current - startOfIS);

        return timeSinceStart.Days % 7;
    }

    public static bool writeTodaySupply(string[] products)
    {
        currentDay = getCurrentDay();
        if (initStep < 1)
        {
            Dalamud.Chat.PrintError("Trying to run solver before solver initiated");
            return false;
        }
        else if (initStep > 1)
            return true;

        bool needToWrite = (currentDay == 0 && importer.needNewWeekData(getCurrentWeek())) || (currentDay > 0 && importer.needNewTodayData(currentDay));
        if (!needToWrite)
            return true;


        Dalamud.Chat.Print("Trying to write supply info starting with " + products[0]);
        if (isProductsValid(products))
        {
            if (currentDay == 0)
            {
                importer.writeWeekStart(products);
            }
            else
            {
                importer.writeNewSupply(products, currentDay);
            }
            return true;
        }
        else
            Dalamud.Chat.PrintError("Can't import supply. Please talk to the Tactful Taskmaster on your Island Sanctuary, open the Supply/Demand window, then reopen /workshop!");
        return false;
        
    }

    private static bool isProductsValid(string[] products)
    {
        int numNE = 0;
        foreach(string product in products)
        {
            if (product.Contains("Nonexistent"))
            {
                //Dalamud.Chat.Print("Found NE row: "+product);
                numNE++;
            }

            if (numNE > 5)
                return false;
        }

        return true;
    }
}
