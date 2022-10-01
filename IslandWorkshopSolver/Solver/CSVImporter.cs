using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Logging;

namespace IslandWorkshopSolver.Solver;

public class CSVImporter
{
    public PeakCycle[] lastWeekPeaks;
    public Popularity[] currentPopularity;
    public List<List<ObservedSupply>> observedSupplies;
    public List<int> observedSupplyHours;
    public List<EndDaySummary> endDays;
    public PeakCycle[] currentPeaks;
    private string rootPath;
    private int currentWeek;
    public CSVImporter(string root, int week)
    {
        lastWeekPeaks = new PeakCycle[Solver.items.Count];
        currentPopularity = new Popularity[Solver.items.Count];
        currentPeaks = new PeakCycle[Solver.items.Count];
        observedSupplies = new List<List<ObservedSupply>>();
        endDays = new List<EndDaySummary>();
        rootPath = root;
        currentWeek = week;
        observedSupplyHours = new List<int>();
        initSupplyData();
    }

    public void writeWeekStart(string[] products)
    {
        //Make new CSV for current week
        string path = getPathForWeek(currentWeek);
        if (File.Exists(path))
        {
            PluginLog.LogError("This week's file already exists at {0} ", path);
            return;
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
                    newFileContents[itemIndex] = productInfo[0] + "," + productInfo[1] + "," + productInfo[2] + "," + productInfo[3];
                }
            }
            newFileContents[products.Length + 1] = ",," + Solver.getCurrentHour();

            File.WriteAllLines(path, newFileContents);
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Error writing file {0}", path);
        }
    }

    public bool needNewWeekData(int currentWeek)
    {
        this.currentWeek = currentWeek;
        string path = getPathForWeek(currentWeek);
        return !File.Exists(path);
    }

    public bool needNewTodayData(int currentDay)
    {
        return observedSupplies[0].Count <= currentDay;
    }

    public void writeNewSupply(string[] products, int currentDay)
    {
        string path = getPathForWeek(currentWeek);
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
            for (int itemIndex = 0; itemIndex < Solver.items.Count; itemIndex++)
            {
                string currentFileLine = fileInfo[itemIndex];
                string[] fileItemInfo = currentFileLine.Split(',');
                if (fileItemInfo.Length == column) //We're ready for today's info
                {
                    changedFile = true;
                    string[] productInfo = products[itemIndex].Split('\t');
                    if (productInfo.Length >= 4)
                    {
                        currentFileLine = currentFileLine + "," + productInfo[2] + "," + productInfo[3];
                        fileInfo[itemIndex] = currentFileLine;
                    }
                }
                else if (fileItemInfo.Length > column+1 && fileItemInfo[column].Length == 0)
                {
                    changedFile = true;
                    string[] productInfo = products[itemIndex].Split('\t');
                    if (productInfo.Length >= 4)
                    {
                        fileItemInfo[column] = productInfo[2];
                        fileItemInfo[column + 1] = productInfo[3];
                        fileInfo[itemIndex] = String.Join(",",fileItemInfo);
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
                int lastWroteHour = Solver.getCurrentHour();

                if (Solver.items.Count >= newFileInfo.Count()) //Missing two summary rows
                {
                    newFileInfo.Add("");
                    newFileInfo.Add("");
                }
                else if (Solver.items.Count + 1 >= newFileInfo.Count()) //Missing one summary row
                    newFileInfo.Add("");

                string summaryRow = newFileInfo[Solver.items.Count + 1];
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
                newFileInfo[Solver.items.Count + 1] = summaryRow;


                File.WriteAllLines(path, newFileInfo);
                initSupplyData();
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

    public void writeEndDay(int day, int groove, int gross, int net, List<Item> crafts)
    {
        PluginLog.LogDebug("Writing end day for day " + (day+1));

        int column = (day+1) * 3 + 1;

        string path = rootPath + "\\Week" + (Solver.week) + "Supply.csv";

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
                for (int c = 0; c < Solver.items.Count; c++)
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

                        sb.Append(Solver.items[c].craftedPerDay[day]);
                        /*if (Solver.items[c].craftedPerDay[day] > 0)
                            PluginLog.LogDebug("Adding " + Solver.items[c].craftedPerDay[day] + "x " + Solver.items[c].item + " to end day summary");*/
                        updated.Add(sb.ToString());
                    }
                    else
                    {
                        split[column] = "" + Solver.items[c].craftedPerDay[day];
                        string joined = String.Join(",", split);
                        //PluginLog.LogDebug("Column set to " + split[column]+", new row: "+joined);
                        updated.Add(joined);
                    }
                }

                PluginLog.LogDebug("Added all num crafted");
            }
            else
            {
                for(int i=0;i<Solver.items.Count;i++)
                {
                    updated.Add(original[i]);
                }
            }

            //Handle summary lines
            if(Solver.items.Count < original.Length)
            {
                string orig = original[Solver.items.Count];
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

            if (Solver.items.Count + 1 < original.Length)
            {
                string orig = original[Solver.items.Count + 1];
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
    public void writeCurrentPeaks(int week)
    {
        string path = rootPath +"\\Week" + (week) + "Supply.csv";

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
                string orig = original[c];
                string[] split = orig.Split(",");
                if (split.Length < 21)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(orig);
                    int commasToAdd = 21 - split.Length;
                    for (int i = 0; i < commasToAdd; i++)
                        sb.Append(",");

                    sb.Append(Solver.items[c].peak);
                    updated.Add(sb.ToString());
                }
                else
                {
                    split[20] = Solver.items[c].peak.ToString();
                    updated.Add(String.Join(",", split));
                    PluginLog.LogWarning("Trying to write new peaks but we already have them? " + orig);
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

    public void initSupplyData() 
    {
        string path = getPathForWeek(currentWeek - 1);
        PluginLog.LogDebug("Looking for file at " + path);

        if (currentWeek > 1)
        {
            if (!File.Exists(path))
            {
                PluginLog.LogError("No file found with old peak data at " + path);
                return;
            }

            
            string[] fileInfoOld = File.ReadAllLines(path);
            for(int c=0; c<Solver.items.Count; c++)
            {
                string line = fileInfoOld[c];

                string[] values = line.Split(",");

                if (values.Length > 20)
                {
                    string peak = values[20].Replace(" ", "");

                    PeakCycle peakEnum = Enum.Parse<PeakCycle>(peak);
                    //PluginLog.LogDebug("Last week's peak: " + peak);
                    lastWeekPeaks[c] = peakEnum;
                }
            }
            
        }
        else
        {
            for (int c = 0; c < lastWeekPeaks.Length; c++)
            {
                lastWeekPeaks[c] = PeakCycle.Cycle2Weak;
            }
            lastWeekPeaks[(int)Item.Barbut] = PeakCycle.Cycle7Strong;
            lastWeekPeaks[(int)Item.Tunic] = PeakCycle.Cycle7Strong;
            lastWeekPeaks[(int)Item.Brush] = PeakCycle.Cycle3Strong;

        }

        path = getPathForWeek(currentWeek);

        observedSupplies.Clear();
        endDays.Clear();
        observedSupplyHours.Clear();

        if (!File.Exists(path))
        {
            PluginLog.LogError("No file found with observed data at " + path);
            return;
        }     

       
            string[] fileInfo = File.ReadAllLines(path);
            
            for (int c = 0; c <= 20; c += 3)
            {
                if (c == 3)
                    c--;
                List<int> numCrafted = new List<int>();
                for (int row = 0; row <= Solver.items.Count && row <fileInfo.Length; row++)
                {
                    string line = fileInfo[row];
                    string[] values = line.Split(",");
                    if(values.Length <= c) //This can happen with summary rows
                    {
                        continue;
                    }
                   
                    
                    string data1 = values[c];
                    string data2 = "";
                    string data3 = "";
                    if (c + 1 < values.Length)
                        data2 = values[c + 1];
                    if (c + 2 < values.Length)
                        data3 = values[c + 2];

                    bool parseAsSummary = row == Solver.items.Count;
                    //PluginLog.LogDebug("Parsing column " + c+" d1: "+data1+" d2: "+data2+" d3: "+data3 +" summary: "+parseAsSummary);


                    if (row == Solver.items.Count) //Summary row
                    {
                        if(c > 0)
                        {
                            string craftsStr = "";
                        string hourRecorded = "";
                            if(fileInfo.Length>row+1)
                            {
                                string itemRow = fileInfo[row + 1];
                                string[] itemValues = itemRow.Split(",");
                                if (itemValues.Length > c)
                                    hourRecorded = itemValues[c];
                                if(itemValues.Length > c+2)
                                {
                                    craftsStr = itemValues[c + 2];
                                }
                            }
                            addSummaryValues(numCrafted, data1, data2, data3, hourRecorded, craftsStr);
                        }

                        continue;
                    }

                    switch (c)
                    {
                        case 0:
                            data2 = data2.Replace(" ", "");
                            Enum.TryParse(data2, out Popularity pop);
                            currentPopularity[row] = pop;
                            break;
                        case 2:
                            if(Enum.TryParse(data1, out Supply supp1) && Enum.TryParse(data2, out DemandShift demand1))
                            {
                                ObservedSupply ob = new ObservedSupply(supp1, demand1);
                                observedSupplies.Add(new List<ObservedSupply>());
                                observedSupplies[row].Add(ob);
                            }                            
                            int.TryParse(data3, out int crafted1);
                            numCrafted.Add(crafted1);
                            break;
                        case 5:
                        if(Enum.TryParse(data1, out Supply supp2) && Enum.TryParse(data2, out DemandShift demand2))
                        {
                            ObservedSupply ob = new ObservedSupply(supp2, demand2);
                            observedSupplies[row].Add(ob);
                        }
                            
                            int.TryParse(data3, out int crafted2);
                            numCrafted.Add(crafted2);
                            break;
                        case 8:
                        if (Enum.TryParse(data1, out Supply supp3) && Enum.TryParse(data2, out DemandShift demand3))
                        {
                            ObservedSupply ob = new ObservedSupply(supp3, demand3);
                            observedSupplies[row].Add(ob);
                        }
                        int.TryParse(data3, out int crafted3);
                            numCrafted.Add(crafted3);
                            break;
                        case 11:
                        if (Enum.TryParse(data1, out Supply supp4) && Enum.TryParse(data2, out DemandShift demand4))
                        {
                            ObservedSupply ob = new ObservedSupply(supp4, demand4);
                            observedSupplies[row].Add(ob);
                        }
                        int.TryParse(data3, out int crafted4);
                            numCrafted.Add(crafted4);
                            break;
                        case 14:
                            int.TryParse(data3, out int crafted5);
                            numCrafted.Add(crafted5);
                            break;
                        case 17:
                            int.TryParse(data3, out int crafted6);
                            numCrafted.Add(crafted6);
                            break;
                        case 20:
                            data1 = data1.Replace(" ", "");
                            Enum.TryParse(data1, out PeakCycle peak);
                            currentPeaks[row] = peak;
                            break;
                    }
                }
            }
        
    }

    private void addSummaryValues(List<int> crafted, string data1, string data2, string data3, string hourRecorded, string craftsStr)
    {
        PluginLog.LogDebug("Adding summary row of groove {0}, gross {1], net {2}, hourRecorded: {3}, and crafts {4}",
            data1, data2, data3, hourRecorded, craftsStr);
        int.TryParse(data1, out int groove);
        int.TryParse(data2, out int gross);
        int.TryParse(data3, out int net);
        int.TryParse(hourRecorded, out int currentHour);
        string[] items = craftsStr.Split(";");
        List<Item> schedule = new List<Item>();
        foreach (var itemStr in items)
        {
            Enum.TryParse(itemStr, out Item item);
            schedule.Add(item);
        }
        endDays.Add(new EndDaySummary(crafted, groove, gross, net, schedule));
        observedSupplyHours.Add(currentHour);
    }

    public string getPathForWeek(int week)
    {
        return rootPath + "\\" + "Week" + week + "Supply.csv";
    }

}
