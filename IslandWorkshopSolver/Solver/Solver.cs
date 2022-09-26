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

    private static int groove;
    private static int totalGross;
    private static int totalNet;
    public static bool rested;

    public static bool verboseCalculatorLogging = false;
    public static bool verboseSolverLogging = false;
    private static int alternatives = 0;
    public static int groovePerDay = 45;
    private static int islandRank = 10;
    public static double materialWeight = 0.5;
    private static CSVImporter importer;
    public static int week = 5;

    static public void RunSolver(string root)
    {
        //TODO: Figure out how to handle D2 because no one's going to craft things D1 to find out
        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        groove = 0;
        totalGross = 0;
        totalNet = 0;
        rested = false;

        int dayToSolve = 5;

        SupplyHelper.DefaultInit();
        PopularityHelper.DefaultInit();
        RareMaterialHelper.DefaultInit();
        initItems();
        importer = new CSVImporter(root, week);
        setInitialFromCSV();
        for (int i = 1; i < dayToSolve; i++)
            setObservedFromCSV(i);
        if(importer.endDays.Count >= dayToSolve && dayToSolve > 0)
        {
            var prevDaySummary = importer.endDays[dayToSolve - 1];
            Dalamud.Chat.Print("previous day summary: " + prevDaySummary);
            groove = prevDaySummary.endingGroove;
            if(prevDaySummary.crafts != null && dayToSolve - 1 > 0)
            {
                var twoDaysAgo = importer.endDays[dayToSolve - 2];
                CycleSchedule yesterdaySchedule = new CycleSchedule(dayToSolve - 1, twoDaysAgo.endingGroove);
                yesterdaySchedule.setForAllWorkshops(prevDaySummary.crafts);
                int gross = yesterdaySchedule.getValue();

                int net = gross - yesterdaySchedule.getMaterialCost();

                importer.writeEndDay(dayToSolve - 1, groove, gross, net, null);
                totalGross += gross;
                totalNet += net;
            }
        }

        //Dalamud.Chat.Print("we made " + groove + " groove yesterday");
        
        if (dayToSolve == 1)
            addDay(new List<Item>(), 0);

        if (dayToSolve<4)
        {
            addOrRest(getBestSchedule(dayToSolve, null), dayToSolve);
        }
        else
        {
            if (importer.currentPeaks == null || importer.currentPeaks[0] == Unknown)
                importer.writeCurrentPeaks(week);
            if (dayToSolve == 4)
                setLateDays();
            else if (dayToSolve == 5)
                setLastTwoDays();
            else
                addOrRest(getBestSchedule(dayToSolve, null), dayToSolve);
        }

        Dalamud.Chat.Print("Week total: " + totalGross + " (" + totalNet + ")\n" + "Took " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time) + "ms.");

    }

    private static void addOrRest(KeyValuePair<WorkshopSchedule, int> rec, int day)
    {
        if (!rested && isWorseThanAllFollowing(rec, day))
        {
            printRestDayInfo(rec.Key.getItems(), day);
            rested = true;
            addDay(new List<Item>(), day);
        }
        else
        {

            addDay(rec.Key.getItems(), day);
        }
    }

    private static void printRestDayInfo(List<Item> rec, int day)
    {
        CycleSchedule restedDay = new CycleSchedule(day, groove);
        restedDay.setForAllWorkshops(rec);
        Dalamud.Chat.Print("Think we're guaranteed to make more than " + restedDay.getValue() + " with " + String.Join(", ",rec) + ". Rest day " + (day + 1));
    }

    private static void setLateDays()
    {
        KeyValuePair<WorkshopSchedule, int> cycle5Sched = getBestSchedule(4, null);
        KeyValuePair<WorkshopSchedule, int> cycle6Sched = getBestSchedule(5, null);
        KeyValuePair<WorkshopSchedule, int> cycle7Sched = getBestSchedule(6, null);

        //I'm just hardcoding this, This could almost certainly be improved

        List<KeyValuePair<WorkshopSchedule, int>> endOfWeekSchedules = new List<KeyValuePair<WorkshopSchedule, int>>();
        endOfWeekSchedules.Add(cycle5Sched);
        endOfWeekSchedules.Add(cycle6Sched);
        endOfWeekSchedules.Add(cycle7Sched);

        int bestDay = -1;
        int bestDayValue = -1;
        int worstDay = -1;
        int worstDayValue = -1;


        for (int i = 0; i < 3; i++)
        {
            int value = endOfWeekSchedules[i].Value;
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
            //Dalamud.Chat.Print("Day 5 is best. Adding as-is");
            addDay(cycle5Sched.Key.getItems(), 4);

            if (worstDay == 5)
            {
                //Day 6 is worst, so recalculate it according to day 7
                KeyValuePair<WorkshopSchedule, int> recalcedCycle7Sched = getBestSchedule(6, null);

                KeyValuePair<WorkshopSchedule, int> recalced6Sched = getBestSchedule(5, new HashSet<Item>(recalcedCycle7Sched.Key.getItems()));
                //Dalamud.Chat.Print("Recalcing day 7");
                if (rested)
                {
                    //Dalamud.Chat.Print("Recalcing day 6 using only day 7's requirements as verboten and adding");
                    addDay(recalced6Sched.Key.getItems(), 5);
                }
                else
                {
                    printRestDayInfo(recalced6Sched.Key.getItems(), 5);
                }
                // Dalamud.Chat.Print("Adding day 7");
                addDay(recalcedCycle7Sched.Key.getItems(), 6);
            }
            else
            {
                //Dalamud.Chat.Print("Day 6 is second best, just recalcing and adding");
                addDay(getBestSchedule(5, null).Key.getItems(), 5);

                KeyValuePair<WorkshopSchedule, int> recalced7Sched = getBestSchedule(6, null);
                if (rested)
                {
                    // Dalamud.Chat.Print("Day 6 is second best, just recalcing and adding 7 too");
                    addDay(recalced7Sched.Key.getItems(), 6);
                }
                else
                {
                    printRestDayInfo(recalced7Sched.Key.getItems(), 6);
                }
            }
        }
        else if (bestDay == 6) //Day 7 is best
        {
            //Dalamud.Chat.Print("Day 7 is best");
            HashSet<Item> reserved7Items = new HashSet<Item>(cycle7Sched.Key.getItems());
            if (worstDay == 4 || rested) //Day 6 is second best or we're using all the days anyway
            {
                //Dalamud.Chat.Print("Day 6 is second best or we're using all the days anyway. Recalcing 6 based on 7.");
                //Recalculate it in case it's better just reserving day 7
                KeyValuePair<WorkshopSchedule, int> recalcedCycle6Sched = getBestSchedule(5, reserved7Items);
                //Dalamud.Chat.Print("Recalced 6: "+String.Join(", ",recalcedCycle6Sched.Key)+" value: "+recalcedCycle6Sched.Value);
                HashSet<Item> reserved67Items = new HashSet<Item>();
                reserved67Items.UnionWith(reserved7Items);
                reserved67Items.UnionWith(recalcedCycle6Sched.Key.getItems());

                if (rested)
                {
                    KeyValuePair<WorkshopSchedule, int> recalcedCycle5Sched = getBestSchedule(4, reserved67Items);
                    //Dalamud.Chat.Print("Recalced 5: "+String.Join(", ",recalcedCycle5Sched.Key)+" value: "+recalcedCycle5Sched.Value);
                    //Dalamud.Chat.Print("Recalcing 5 based on 6. Is it better?");


                    //Only use the recalculation if it doesn't ruin D5 too badly
                    if (recalcedCycle5Sched.Value + recalcedCycle6Sched.Value > cycle5Sched.Value + cycle6Sched.Value)
                    {
                        //Dalamud.Chat.Print("It is! Using recalced 5");
                        addDay(recalcedCycle5Sched.Key.getItems(), 4);
                    }

                    else
                    {
                        addDay(cycle5Sched.Key.getItems(), 4);
                    }
                    //Dalamud.Chat.Print("Recalcing 6 AGAIN just in case 5 changed it, still only forbidding things used day 7");
                    addDay(getBestSchedule(5, reserved7Items).Key.getItems(), 5);
                }
                else
                {
                    printRestDayInfo(cycle5Sched.Key.getItems(), 4);
                    addDay(recalcedCycle6Sched.Key.getItems(), 5);
                }
            }
            if (worstDay == 5) //Day 5 is second best and we aren't using day 6
            {
                printRestDayInfo(cycle6Sched.Key.getItems(), 5);
                ///Dalamud.Chat.Print("Day 6 isn't being used so just recalc 5 based on day 7");
                addDay(getBestSchedule(4, reserved7Items).Key.getItems(), 4);
            }
            //Dalamud.Chat.Print("Adding recalced day 7");
            addDay(getBestSchedule(6, null).Key.getItems(), 6);

        }
        else //Best day is Day 6
        {
            //Dalamud.Chat.Print("Day 6 is best");
            if (rested || worstDay != 4)
            {
                //Dalamud.Chat.Print("Adding day 5 as-is");
                addDay(cycle5Sched.Key.getItems(), 4);
            }
            else
                printRestDayInfo(cycle5Sched.Key.getItems(), 4);
            //Dalamud.Chat.Print("Recalcing day 6 and adding");
            addDay(getBestSchedule(5, null).Key.getItems(), 5);

            KeyValuePair<WorkshopSchedule, int> recalcedCycle7Sched = getBestSchedule(6, null);
            if (rested || worstDay != 6)
            {
                //Dalamud.Chat.Print("Recalcing day 7 and adding");
                addDay(recalcedCycle7Sched.Key.getItems(), 6);
            }
            else
                printRestDayInfo(recalcedCycle7Sched.Key.getItems(), 6);
        }
    }

    private static void setLastTwoDays()
    {
        KeyValuePair<WorkshopSchedule, int> cycle6Sched = getBestSchedule(5, null);
        KeyValuePair<WorkshopSchedule, int> cycle7Sched = getBestSchedule(6, null);

        KeyValuePair<WorkshopSchedule, int> recalcedCycle6Sched = getBestSchedule(5, cycle7Sched.Key.getItems().ToHashSet());
        KeyValuePair<WorkshopSchedule, int> trueCycle6Sched = recalcedCycle6Sched.Value > cycle6Sched.Value ? recalcedCycle6Sched : cycle6Sched;

        if (trueCycle6Sched.Value >= cycle7Sched.Value || rested)
        {
            addDay(trueCycle6Sched.Key.getItems(), 5);
            if (rested)
                addDay(cycle7Sched.Key.getItems(), 6);
        }
        else
        {
            printRestDayInfo(trueCycle6Sched.Key.getItems(), 5);
            addDay(cycle7Sched.Key.getItems(), 6);
        }


    }

    private static bool isWorseThanAllFollowing(KeyValuePair<WorkshopSchedule, int> rec, int day)
    {
        int worstInFuture = 99999;
        if (verboseSolverLogging)
            Dalamud.Chat.Print("Comparing d" + (day + 1) + " (" + rec.Value + ") to worst-case future days");
        HashSet<Item> reservedSet = new HashSet<Item>();
        for (int d = day + 1; d < 7; d++)
        {
            KeyValuePair<WorkshopSchedule, int> solution;
            if (day == 3 && d == 4) //We have a lot of info about this specific pair so we might as well use it
                solution = getD5EV();
            else
                solution = getBestSchedule(d, reservedSet, false);
            if (verboseSolverLogging)
                Dalamud.Chat.Print("Day " + (d + 1) + ", crafts: " + String.Join(", ", solution.Key.getItems()) + " value: " + solution.Value);
            worstInFuture = Math.Min(worstInFuture, solution.Value);
            reservedSet.UnionWith(solution.Key.getItems());
        }
        if (verboseSolverLogging)
            Dalamud.Chat.Print("Worst future day: " + worstInFuture);

        return rec.Value <= worstInFuture;
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

            int toAdd = solution.Key.getValueWithGrooveEstimate(4, groove);
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

    public static void addDay(List<Item> crafts, int day)
    {

        Dalamud.Chat.Print("Day " + (day + 1) + ", crafts: " + String.Join(", ", crafts));

        addDay(crafts, crafts, crafts, day);
    }

    public static void addDay(List<Item> crafts0, List<Item> crafts1, List<Item> crafts2, int day)
    {
        CycleSchedule schedule = new CycleSchedule(day, groove);
        schedule.setWorkshop(0, crafts0);
        schedule.setWorkshop(1, crafts1);
        schedule.setWorkshop(2, crafts2);

        int gross = schedule.getValue();
        totalGross += gross;

        int net = gross - schedule.getMaterialCost();
        totalNet += net;
        int startingGroove = groove;
        groove = schedule.endingGroove;

        schedule.GrooveToZero();
        bool oldVerbose = verboseCalculatorLogging;
        verboseCalculatorLogging = false;
        Dalamud.Chat.Print("day " + (day + 1) + " total, 0 groove: " + schedule.getValue() + ". Starting groove " + startingGroove + ": " + gross + ", net " + net + ".");
        verboseCalculatorLogging = oldVerbose;

        foreach(var kvp in schedule.numCrafted)
        {
            items[(int)kvp.Key].setCrafted(kvp.Value, day);
        }

        if (schedule.hasAnyUnsurePeaks())
            importer.writeEndDay(day, groove, 0, 0, crafts0);
        else
            importer.writeEndDay(day, groove, gross, net, null);
    }

    private static KeyValuePair<WorkshopSchedule, int> getBestScheduleAndAlts(Dictionary<WorkshopSchedule, int> schedules)
    {
        var scheduleEnum = schedules.OrderByDescending(kvp => kvp.Value).GetEnumerator();

        scheduleEnum.MoveNext();
        KeyValuePair<WorkshopSchedule, int> bestSchedule = scheduleEnum.Current;


        if (alternatives > 0)
        {
            Dalamud.Chat.Print("Best rec: " + String.Join(", ", bestSchedule.Key.getItems()) + ": " + bestSchedule.Value);
            int count = 0;
            for(int c=0; c< alternatives && scheduleEnum.MoveNext(); c++)
            {
                KeyValuePair<WorkshopSchedule, int> alt = scheduleEnum.Current;
                Dalamud.Chat.Print("Alternative rec: " + String.Join(", ", alt.Key.getItems()) + ": " + alt.Value);
                count++;
            }
        }

        return bestSchedule;
    }

    private static KeyValuePair<WorkshopSchedule, int> getBestSchedule(int day, HashSet<Item>? reservedForLater, bool allowAllOthers = true)
    {
        var fourHour = new List<ItemInfo>();
        var eightHour = new List<ItemInfo>();
        var sixHour = new List<ItemInfo>();

        if (reservedForLater == null)
            allowAllOthers = false;

        foreach(ItemInfo item in items)
        {
            List<ItemInfo>? bucket = null;

            if (reservedForLater != null && reservedForLater.Contains(item.item))
                continue;

            if (item.time == 4 && item.rankUnlocked <= islandRank && (allowAllOthers || item.peaksOnOrBeforeDay(day, true)))
                bucket = fourHour;
            else if (item.time == 6 && item.rankUnlocked <= islandRank && (allowAllOthers || item.peaksOnOrBeforeDay(day,false)))
                bucket = sixHour;
            else if (item.time == 8 && item.rankUnlocked <= islandRank && (allowAllOthers || item.peaksOnOrBeforeDay(day, false)))
                bucket = eightHour;            

            if(bucket!=null)
                bucket.Add(item);
        }


        Dictionary<WorkshopSchedule, int> safeSchedules = new Dictionary<WorkshopSchedule, int>();

        //Find schedules based on 8-hour crafts
        var eightEnum = eightHour.GetEnumerator();
        while (eightEnum.MoveNext())
        {
            var topItem = eightEnum.Current;
            if (verboseSolverLogging)
                Dalamud.Chat.PrintError("Building schedule around : " + topItem.item + ", peak: "+topItem.peak);


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
                    if(verboseSolverLogging)
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

        return getBestScheduleAndAlts(safeSchedules);
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

        int value = workshop.getValueWithGrooveEstimate(day, groove);
        //Only add if we don't already have one with this schedule or ours is better
        int oldValue = (safeSchedules.TryGetValue(workshop, out int dictValue)?dictValue : -1);

        if (oldValue < value)
        {
            if (verboseSolverLogging)
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
                items[i].addObservedDay(ob);
            }
            if (hasDaySummary && importer.endDays[day].craftedItems() > i)
                items[i].setCrafted(importer.endDays[day].getCrafted(i), day);
        }
        
        if(hasDaySummary && importer.endDays[day].endingGross > -1)
        {
            //Dalamud.Chat.Print("Adding totals from day " + day + ": " + importer.endDays[day]);
            totalGross += importer.endDays[day].endingGross;
            totalNet += importer.endDays[day].endingNet;
            if (importer.endDays[day].endingGross == 0)
            {
                //Dalamud.Chat.Print("Rested day " + (day + 1));
                rested = true;
            }
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
}
