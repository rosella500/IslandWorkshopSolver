using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Logging;

namespace Beachcomber.Solver;

public class CSVImporter
{
    public PeakCycle[] lastWeekPeaks;
    public Popularity[] currentPopularity;
    public List<Dictionary<int,ObservedSupply>> observedSupplies;
    public List<int> observedSupplyHours;
    public List<EndDaySummary> endDays;
    public PeakCycle[] currentPeaks;
    private string rootPath;
    private int currentWeek;

    //Don't ever call this, it's just there to make the compiler happy
    public CSVImporter()
    {
        lastWeekPeaks = new PeakCycle[0];
        currentPopularity = new Popularity[0];
        currentPeaks = new PeakCycle[0];
        observedSupplies = new List<Dictionary<int, ObservedSupply>>();
        endDays = new List<EndDaySummary>();
        observedSupplyHours = new List<int>();
        rootPath = "";
    }
    public CSVImporter(string root, int week)
    {
        lastWeekPeaks = new PeakCycle[Solver.Items.Count];
        currentPopularity = new Popularity[Solver.Items.Count];
        currentPeaks = new PeakCycle[Solver.Items.Count];
        observedSupplies = new List<Dictionary<int, ObservedSupply>>();
        endDays = new List<EndDaySummary>();
        rootPath = root;
        currentWeek = week;
        observedSupplyHours = new List<int>();
        InitSupplyData();
    }

    public void WriteWeekStart(string[] products)
    {
        //Make new CSV for current week
        string path = GetPathForWeek(currentWeek);
        if (File.Exists(path))
        {
            PluginLog.LogWarning("This week's file already exists at {0} ", path);
        }
        PluginLog.LogDebug("Starting to write a file to {0}", path);
        try
        {
            string[] newFileContents = new string[products.Length+2];
            for (int itemIndex = 0; itemIndex < products.Length; itemIndex++)
            {
                string[] productInfo = products[itemIndex].Split('\t');

                if (productInfo.Length >= 4)
                {
                    newFileContents[itemIndex] = productInfo[0] + "," + productInfo[1] ;
                    ParsePopularity(itemIndex, productInfo[1]);
                }
            }

            File.WriteAllLines(path, newFileContents);
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error writing file {0}", path);
        }
    }

    public bool NeedNewWeekData(int currentWeek)
    {
        this.currentWeek = currentWeek;
        string path = GetPathForWeek(currentWeek);
        return !File.Exists(path);
    }

    public bool NeedNewTodayData(int currentDay)
    {
        return observedSupplies.Count == 0 || !observedSupplies[0].ContainsKey(currentDay);
    }

    public bool NeedToOverwriteTodayData(int currentDay, string[] products)
    {
        int i = 0;

        if (observedSupplies.Count == 0 || !observedSupplies[0].ContainsKey(currentDay))
            return false;

        foreach (var observedDict in observedSupplies)
        {
            int previousToday = (int)observedDict[currentDay].supply;
            if (products.Length > i)
            {
                var split = products[i].Split('\t');
                if (split.Length > 2)
                {
                    int newToday = int.Parse(split[2]);
                    //If we have data for today, but the data we're trying to write has things going down, we somehow wrote bad data and should overwrite it now
                    if (newToday < previousToday)
                        return true;
                }
            }
            i++;
        }

        return false;
    }

    public void WriteNewSupply(string[] products, int currentDay)
    {
        string path = GetPathForWeek(currentWeek);
        if (!File.Exists(path))
        {
            PluginLog.LogError("No file found to add supply to at " + path);
            return;
        }

        try
        {
            string[] fileInfo = File.ReadAllLines(path);
            //itemName, popularity, supply, shift, supply, shift, etc.

            bool changedFile = false;
            int column = 2 + (currentDay * 3);
            for (int itemIndex = 0; itemIndex < Solver.Items.Count; itemIndex++)
            {
                string currentFileLine = fileInfo[itemIndex];
                string[] fileItemInfo = currentFileLine.Split(',');
                if (fileItemInfo.Length < column + 1) //We're ready for today's info
                {
                    changedFile = true;
                    string[] productInfo = products[itemIndex].Split('\t');
                    if (productInfo.Length >= 4)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append(currentFileLine);
                        int commasToAdd = column + 1 - fileItemInfo.Length;
                        for (int i = 0; i < commasToAdd; i++)
                            sb.Append(',');
                        sb.Append(productInfo[2]).Append(',').Append(productInfo[3]);
                        fileInfo[itemIndex] = sb.ToString();
                        ParseObservedSupply(itemIndex, currentDay, productInfo[2], productInfo[3]);
                    }
                }
                else if (fileItemInfo.Length > column+1)
                {
                    changedFile = true;
                    string[] productInfo = products[itemIndex].Split('\t');
                    if (productInfo.Length >= 4)
                    {
                        fileItemInfo[column] = productInfo[2];
                        fileItemInfo[column + 1] = productInfo[3];
                        fileInfo[itemIndex] = String.Join(",",fileItemInfo);
                        ParseObservedSupply(itemIndex, currentDay, productInfo[2], productInfo[3]);
                    }
                }
                else
                {
                    PluginLog.LogWarning("Trying to write to column " + column + " but length: " + fileItemInfo.Length);
                }
            }
            if (changedFile)
            {
                List<string> newFileInfo = new List<string>(fileInfo);
                //Add summary line
                int lastWroteHour = Solver.GetCurrentHour();
                while (observedSupplyHours.Count <= currentDay)
                    observedSupplyHours.Add(lastWroteHour);

                if (Solver.Items.Count >= newFileInfo.Count()) //Missing two summary rows
                {
                    newFileInfo.Add("");
                    newFileInfo.Add("");
                }
                else if (Solver.Items.Count + 1 >= newFileInfo.Count()) //Missing one summary row
                    newFileInfo.Add("");

                string summaryRow = newFileInfo[Solver.Items.Count + 1];
                string[] split = summaryRow.Split(',');
                if (split.Length < column + 1)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(summaryRow);
                    int commasToAdd = column + 1 - split.Length;
                    for (int i = 0; i < commasToAdd; i++)
                        sb.Append(',');
                    sb.Append(lastWroteHour);
                    summaryRow = sb.ToString();
                }
                else
                {
                    split[column] = lastWroteHour.ToString();
                    summaryRow = String.Join(',', split);
                }
                newFileInfo[Solver.Items.Count + 1] = summaryRow;

                File.WriteAllLines(path, newFileInfo);
            }
            else
            {
                PluginLog.LogDebug("Supply data for day " + (currentDay + 1) + " already found in sheet. Did not write anything.");
            }
        }
        catch (Exception e)
        {
            PluginLog.LogError(e,"Error writing file {0}",path);
        }
    }

    public void WriteEndDay(int day, int groove, int gross, int net, List<Item> crafts)
    {
        PluginLog.LogDebug("Writing end day for day " + (day+1));

        int column = (day+1) * 3 + 1;

        string path = rootPath + "\\Week" + (Solver.Week) + "Supply.csv";

        if (!File.Exists(path))
        {
            PluginLog.LogError("No file found with data at " + path);
            return;
        }
        try
        {
            string[] original = File.ReadAllLines(path);
            PluginLog.LogDebug("Reading end summary CSV. Lines: " + original.Length);

            List<string> updated = new List<string>();

            if(day < 6)
            {
                for (int c = 0; c < Solver.Items.Count; c++)
                {
                    string orig = original[c];
                    string[] split = orig.Split(",");
                    if (split.Length < column + 1)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append(orig);
                        int commasToAdd = column + 1 - split.Length;
                        for (int i = 0; i < commasToAdd; i++)
                            sb.Append(",");

                        sb.Append(Solver.Items[c].craftedPerDay[day]);
                        /*if (Solver.items[c].craftedPerDay[day] > 0)
                            PluginLog.LogDebug("Adding " + Solver.items[c].craftedPerDay[day] + "x " + Solver.items[c].item + " to end day summary");*/
                        updated.Add(sb.ToString());
                    }
                    else
                    {
                        split[column] = "" + Solver.Items[c].craftedPerDay[day];
                        string joined = String.Join(",", split);
                        //PluginLog.LogDebug("Column set to " + split[column]+", new row: "+joined);
                        updated.Add(joined);
                    }
                }

                PluginLog.LogDebug("Added all num crafted");
            }
            else
            {
                for(int i=0;i<Solver.Items.Count;i++)
                {
                    updated.Add(original[i]);
                }
            }

            //Handle summary lines
            if(Solver.Items.Count < original.Length)
            {
                string orig = original[Solver.Items.Count];
                //PluginLog.LogDebug("Summary line exists: " + orig+" trying to write to column "+column);
                string[] split = orig.Split(",");
                if (split.Length < column - 1)
                {
                    //PluginLog.LogDebug("Summary line ends before we need to write, can just append");
                    StringBuilder sb = new StringBuilder(orig);
                    int commasToAdd = column - 1 - split.Length;
                    for (int i = 0; i < commasToAdd; i++)
                        sb.Append(',');

                    sb.Append(groove).Append(',').Append(gross).Append(',').Append(net);
                    updated.Add(sb.ToString());
                    //PluginLog.LogDebug("Adding onto existing row: " + sb.ToString());
                }
                else
                {
                    //PluginLog.LogDebug("Summary line is long enough we have to overwrite values") ;
                    split[column - 2] = "" + groove;
                    if (split.Length > column - 1)
                    {
                        split[column - 1] = "" + gross;

                        if (split.Length >  column)
                        {
                            split[column] = "" + net;
                            updated.Add(String.Join(",", split));
                        }
                        else
                        {
                            StringBuilder sb = new StringBuilder(String.Join(",", split));
                            sb.Append(',').Append(net);
                            updated.Add(sb.ToString());
                        }
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder(String.Join(",", split));
                        sb.Append(',').Append(gross).Append(',').Append(net);
                        updated.Add(sb.ToString());
                    }
                    
                    
                    //PluginLog.LogDebug("Setting existing row's values to " + updated[updated.Count - 1]);
                }
            }
            else
            {
                //PluginLog.LogDebug("Need to add new row for output summaries");
                StringBuilder sb = new StringBuilder();
                int commasToAdd = column - 2;
                for (int i = 0; i < commasToAdd; i++)
                    sb.Append(',');
                sb.Append(groove).Append(',');
                sb.Append(gross).Append(',').Append(net);
                updated.Add(sb.ToString());
                //PluginLog.LogDebug("Adding new row: " + sb.ToString());
            }

            if (Solver.Items.Count + 1 < original.Length)
            {
                string orig = original[Solver.Items.Count + 1];
                //PluginLog.LogDebug("Summary line 2 exists: " + orig+" trying to write to column "+column);
                string[] split = orig.Split(",");
                if (split.Length < column + 1)
                {
                    //PluginLog.LogDebug("Adding to end of file for items ");
                    StringBuilder sb = new StringBuilder();
                    sb.Append(orig);
                    int commasToAdd = column + 1 - split.Length;
                    for (int i = 0; i < commasToAdd; i++)
                        sb.Append(",");

                    sb.Append(String.Join(";", crafts));
                    updated.Add(sb.ToString());
                }
                else
                {
                    split[column] = String.Join(";", crafts);
                    string joined = String.Join(",", split);
                    //PluginLog.LogDebug("Column set to " + split[column]+", new row: "+joined);
                    updated.Add(joined);
                }
            }
            else
            {
                //PluginLog.LogDebug("Need to add new row for output summary 2");
                StringBuilder sb = new StringBuilder();
                int commasToAdd = column + 1;
                for (int i = 0; i < commasToAdd; i++)
                    sb.Append(",");
                
                sb.Append(String.Join(";", crafts));
                updated.Add(sb.ToString());
                //PluginLog.LogDebug("Adding new row: " + sb.ToString());
            }

            PluginLog.LogDebug("Writing " + updated.Count + " lines to the end-day summary list");

            File.WriteAllLines(path, updated);
        }
        catch (Exception e)
        {
            PluginLog.LogError("Error writing num crafts to csv " + path + ": " + e.Message);
        }

    }
    public bool NeedCurrentPeaks()
    {
        for (int i=0; i<currentPeaks.Length; i++)
        {
            var peak = currentPeaks[i];
            if (!peak.IsTerminal() && Solver.Items[i].peak.IsTerminal())
                return true;
        }            

        return false;
    }

    public bool HasAllPeaks()
    {
        foreach(var peak in currentPeaks)
            if (!peak.IsTerminal())
                return false;

        return true;
    }

    public bool HasAllPeaksForward(int day)
    {
        int[] numStrong = new int[7-day];
        int[] numWeak = new int[7-day];
        for(int i=0; i<7-day; i++)
        {
            foreach(var item in Solver.Items)
            {
                int peakNum = (int)item.peak;
                if (peakNum == (i + day) * 2) //Strong peak for this day
                    numStrong[i]++;
                else if (peakNum == (i + day) * 2 - 1)
                    numWeak[i]++;
            }
        }

        PluginLog.Debug("# peaks per day, starting at day {0}: strong: {1}, weak {2}", day + 1, String.Join(", ", numStrong), String.Join(", ", numWeak));

        for(int i=0; i<7-day; i++)
        {
            if(i == numStrong.Length-1) //last day, must be 7
            {
                if (numStrong[i] != 5 || numWeak[i] != 5)
                {
                    PluginLog.Debug("Don't have enough d7 peaks ({0} strong and {1} weak)", numStrong[i], numWeak[i]);
                    return false;
                }
                    
            }
            else
            {
                if (numStrong[i] != 4 || numWeak[i] != 4)
                {
                    PluginLog.Debug("Don't have enough d{2} peaks ({0} strong and {1} weak)", numStrong[i], numWeak[i], i+day+1);
                    return false;
                }
            }
        }



        return false;
    }
    public void WriteCurrentPeaks(int week)
    {
        string path = rootPath + "\\Week" + (week) + "Supply.csv";

        if (!File.Exists(path))
        {
            PluginLog.LogError("No file found with data at " + path);
            return;
        }

        try
        {
            string[] original = File.ReadAllLines(path);
            
            List<string> updated = new List<string>();

            for (int c = 0; c < currentPeaks.Length; c++)
            {
                currentPeaks[c] = Solver.Items[c].peak;
                string orig = original[c];
                string[] split = orig.Split(",");
                if (split.Length < 21)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(orig);
                    int commasToAdd = 21 - split.Length;
                    for (int i = 0; i < commasToAdd; i++)
                        sb.Append(",");

                    sb.Append(Solver.Items[c].peak);
                    updated.Add(sb.ToString());
                }
                else
                {
                    split[20] = Solver.Items[c].peak.ToString();
                    updated.Add(String.Join(",", split));
                    //PluginLog.LogWarning("Trying to write new peaks but we already have them? " + orig);
                }
            }
            for(int c=currentPeaks.Length; c< original.Length;c++)
            {
                updated.Add(original[c]);
            }
            File.WriteAllLines(path, updated);
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error writing new peaks to csv " + path);
        }

    }

    public void InitSupplyData()
    {
        string path = GetPathForWeek(currentWeek - 1);
        if (currentWeek > 1 && File.Exists(path))
        {
            string[] fileInfoOld = File.ReadAllLines(path);
            for (int c = 0; c < Solver.Items.Count; c++)
            {
                string line = fileInfoOld[c];

                string[] values = line.Split(",");

                if (values.Length > 20)
                {
                    ParsePeak(c, values[20], lastWeekPeaks);
                }
            }
        }
        else
        {
            PluginLog.LogWarning("No file found with old peak data at " + path + ". Day 2 prediction is going to be less accurate.");
        }

        path = GetPathForWeek(currentWeek);

        observedSupplies.Clear();
        endDays.Clear();
        observedSupplyHours.Clear();

        if (!File.Exists(path))
        {
            PluginLog.LogError("No file found with observed data at " + path);
            return;
        }


        string[] fileInfo = File.ReadAllLines(path);
        PluginLog.LogDebug("Reading file at {0} with {1} lines", path, fileInfo.Length);

        for (int c = 0; c <= 20; c += 3)
        {
            if (c == 3)
                c--;
            List<int> numCrafted = new List<int>();
            for (int row = 0; row <= Solver.Items.Count && row < fileInfo.Length; row++)
            {
                string line = fileInfo[row];
                string[] values = line.Split(",");

                string data1 = "";
                string data2 = "";
                string data3 = "";
                if (c < values.Length)
                    data1 = values[c];
                if (c + 1 < values.Length)
                    data2 = values[c + 1];
                if (c + 2 < values.Length)
                    data3 = values[c + 2];

                bool parseAsSummary = row == Solver.Items.Count;
                //PluginLog.LogDebug("Parsing column " + c + " d1: " + data1 + " d2: " + data2 + " d3: " + data3 + " summary: " + parseAsSummary);


                if (row == Solver.Items.Count) //Summary row
                {
                    //PluginLog.LogDebug("Found first summary row, looking at column {0} ", c);
                    if (c > 0)
                    {
                        string craftsStr = "";
                        string hourRecorded = "";

                        if (fileInfo.Length > row + 1)
                        {
                            string itemRow = fileInfo[row + 1];

                            string[] itemValues = itemRow.Split(",");
                            if (itemValues.Length > c)
                                hourRecorded = itemValues[c];
                            if (itemValues.Length > c + 2)
                            {
                                craftsStr = itemValues[c + 2];
                            }
                        }
                        AddSummaryValues(numCrafted, data1, data2, data3, hourRecorded, craftsStr);
                    }

                    continue;
                }

                switch (c)
                {
                    case 0:
                        ParsePopularity(row, data2);
                        break;
                    case 2:
                        ParseObservedSupply(row, 0, data1, data2);
                        int.TryParse(data3, out int crafted1);
                        numCrafted.Add(crafted1);
                        break;
                    case 5:
                        ParseObservedSupply(row, 1, data1, data2);

                        int.TryParse(data3, out int crafted2);
                        numCrafted.Add(crafted2);
                        break;
                    case 8:
                        ParseObservedSupply(row, 2, data1, data2);
                        int.TryParse(data3, out int crafted3);
                        numCrafted.Add(crafted3);
                        break;
                    case 11:
                        ParseObservedSupply(row, 3, data1, data2);
                        int.TryParse(data3, out int crafted4);
                        numCrafted.Add(crafted4);
                        break;
                    case 14:
                        ParseObservedSupply(row, 4, data1, data2);
                        int.TryParse(data3, out int crafted5);
                        numCrafted.Add(crafted5);
                        break;
                    case 17:
                        ParseObservedSupply(row, 5, data1, data2);
                        int.TryParse(data3, out int crafted6);
                        numCrafted.Add(crafted6);
                        break;
                    case 20:
                        ParsePeak(row, data1, currentPeaks);
                        break;
                }
            }
        }
    }

    private void ParsePopularity(int index, string popularity)
    {
        if(int.TryParse(popularity, out int popInt) && Enum.IsDefined((Popularity)popInt))
            currentPopularity[index] = (Popularity)popInt;
    }

    private void ParseObservedSupply(int index, int day, string supply, string demandShift)
    {
        while (observedSupplies.Count <= index)
            observedSupplies.Add(new Dictionary<int, ObservedSupply>());


        if (int.TryParse(supply, out int supplyInt) && Enum.IsDefined((Supply)supplyInt) &&
        int.TryParse(demandShift, out int demandInt) && Enum.IsDefined((DemandShift)demandInt))
        {
            ObservedSupply ob = new ObservedSupply((Supply)supplyInt, (DemandShift)demandInt);
            if(observedSupplies[index].ContainsKey(day))
                observedSupplies[index][day] = ob;
            else
                observedSupplies[index].Add(day, ob);
        }
    }

    private void ParsePeak(int index, string peakStr, PeakCycle[] peaks)
    {
        peakStr = peakStr.Replace(" ", "");
        if(Enum.TryParse(peakStr, out PeakCycle peak))
        peaks[index] = peak;
    }


    private void AddSummaryValues(List<int> crafted, string data1, string data2, string data3, string hourRecorded, string craftsStr)
    {
        PluginLog.LogDebug("Adding summary row of groove {0}, gross {1}, net {2}, hourRecorded: {3}, and crafts {4}",
            data1, data2, data3, hourRecorded, craftsStr);
        int.TryParse(data1, out int groove);
        int.TryParse(data2, out int gross);
        int.TryParse(data3, out int net);
        int.TryParse(hourRecorded, out int currentHour);
        string[] items = craftsStr.Split(";");
        List<Item> schedule = new List<Item>();
        foreach (var itemStr in items)
        {
            if(Enum.TryParse(itemStr, out Item item))
                schedule.Add(item);
        }
        endDays.Add(new EndDaySummary(crafted, groove, gross, net, schedule));
        observedSupplyHours.Add(currentHour);
    }

    public string GetPathForWeek(int week)
    {
        return rootPath + "\\" + "Week" + week + "Supply.csv";
    }

}
