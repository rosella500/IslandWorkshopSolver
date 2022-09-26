using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Lumina.Data.Files.LgbFile;

namespace IslandWorkshopSolver.Solver;

public class CSVImporter
{
    public List<List<Item>> allEfficientChains;
    public PeakCycle[] lastWeekPeaks;
    public Popularity[] currentPopularity;
    public List<List<ObservedSupply>> observedSupplies;
    public List<EndDaySummary> endDays;
    public PeakCycle[] currentPeaks;
    private string rootPath;
    public CSVImporter(string root, int week)
    {
        lastWeekPeaks = new PeakCycle[Solver.items.Count];
        currentPopularity = new Popularity[Solver.items.Count];
        currentPeaks = new PeakCycle[Solver.items.Count];
        observedSupplies = new List<List<ObservedSupply>>();
        endDays = new List<EndDaySummary>();
        rootPath = root;

        initSupplyData(week);
    }

    public void writeEndDay(int day, int groove, int gross, int net)
    {
        Dalamud.Chat.Print("Writing end day for day " + day);

        int column = (day+1) * 3 + 1;

        string path = rootPath + "\\Week" + (Solver.week) + "Supply.csv";

        if (!File.Exists(path))
        {
            Dalamud.Chat.PrintError("No file found with data at " + path);
            return;
        }
        try
        {
            string[] original = File.ReadAllLines(path);

            List<string> updated = new List<string>();

            if(day < 3)
            {
                for (int c = 0; c < Solver.items.Count; c++)
                {
                    string orig = original[c];
                    string[] split = orig.Split(",");
                    if (split.Length < column + 1)
                    {
                        Dalamud.Chat.Print("Adding to end of file for numCrafted ");
                        StringBuilder sb = new StringBuilder();
                        sb.Append(orig);
                        int commasToAdd = column + 1 - split.Length;
                        for (int i = 0; i < commasToAdd; i++)
                            sb.Append(",");

                        sb.Append(Solver.items[c].craftedPerDay[day]);
                        updated.Add(sb.ToString());
                    }
                    else
                    {
                        Dalamud.Chat.Print("File already exists, setting column ");
                        split[column] = "" + Solver.items[c].craftedPerDay[day];
                        string joined = String.Join(",", split);
                        Dalamud.Chat.Print("Column set to " + split[column]+", new row: "+joined);
                        updated.Add(joined);
                    }
                }

                Dalamud.Chat.Print("Added all num crafted");
            }

            //Handle summary line
            if(Solver.items.Count < original.Length)
            {
                string orig = original[Solver.items.Count];
                Dalamud.Chat.Print("Summary line exists: " + orig+" trying to write to column "+column);
                string[] split = orig.Split(",");
                if (split.Length < column - 1)
                {
                    Dalamud.Chat.Print("Summary line ends before we need to write, can just append");
                    StringBuilder sb = new StringBuilder(orig);
                    int commasToAdd = column - 1 - split.Length;
                    for (int i = 0; i < commasToAdd; i++)
                        sb.Append(",");

                    sb.Append(groove).Append(',').Append(gross).Append(',').Append(net);
                    updated.Add(sb.ToString());
                    Dalamud.Chat.Print("Adding onto existing row: " + sb.ToString());
                }
                else
                {
                    Dalamud.Chat.Print("Summary line is long enough we have to overwrite values") ;
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
                    
                    
                    Dalamud.Chat.Print("Setting existing row's values to " + updated[updated.Count - 1]);
                }
            }
            else
            {
                Dalamud.Chat.Print("Need to add new row for output summaries");
                StringBuilder sb = new StringBuilder();
                int commasToAdd = column - 2;
                for (int i = 0; i < commasToAdd; i++)
                    sb.Append(',');

                sb.Append(groove).Append(',').Append(gross).Append(',').Append(net);
                updated.Add(sb.ToString());
                Dalamud.Chat.Print("Adding new row: " + sb.ToString());
            }

            File.WriteAllLinesAsync(path, updated);
        }
        catch (Exception e)
        {
            Dalamud.Chat.PrintError("Error writing num crafts to csv " + path + ": " + e.Message);
        }

    }

    public void writeCurrentPeaks(int week)
    {
        string path = rootPath +"\\Week" + (week) + "Supply.csv";

        if (!File.Exists(path))
        {
            Dalamud.Chat.PrintError("No file found with data at " + path);
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
                if (split.Length < 14)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(orig);
                    int commasToAdd = 14 - split.Length;
                    for (int i = 0; i < commasToAdd; i++)
                        sb.Append(",");

                    sb.Append(Solver.items[c].peak);
                    updated.Add(sb.ToString());
                }
                else
                {
                    split[14] = Solver.items[c].peak.ToString();
                    updated.Add(String.Join(",", split));
                    Dalamud.Chat.Print("Trying to write new peaks but we already have them? " + orig);
                }
            }
            File.WriteAllLinesAsync(path, updated);
        }
        catch (Exception e)
        {
            Dalamud.Chat.Print("Error writing new peaks to csv " + path + ": " + e.Message);
        }

    }

    public void initSupplyData(int week)
    {
        string fileName = "Week" + (week - 1) + "Supply.csv";

        string path = rootPath + "\\" + fileName;
        //Dalamud.Chat.Print("Looking for file at " + path);

        if (week > 1)
        {
            if (!File.Exists(path))
            {
                Dalamud.Chat.PrintError("No file found with old peak data at " + path);
                return;
            }
            //Dalamud.Chat.Print("Starting to read file at " + path + " array length " + lastWeekPeaks.Length);

            try
            {
                string[] fileInfo = File.ReadAllLines(path);
                for(int c=0; c<fileInfo.Length; c++)
                {
                    string line = fileInfo[c];

                    string[] values = line.Split(",");

                    if (values.Length >= 11)
                    {
                        string peak = values[13].Replace(" ", "");

                        PeakCycle peakEnum = Enum.Parse<PeakCycle>(peak);
                        //Dalamud.Chat.Print("Last week's peak: " + peak);
                        lastWeekPeaks[c] = peakEnum;
                    }
                }
            }
            catch (Exception e)
            {
                Dalamud.Chat.Print("Error importing csv " + path+ " "+e.GetType()+": " + e.Message);
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

        path = rootPath + "\\Week" + week + "Supply.csv";

        observedSupplies.Clear();
        endDays.Clear();

        if (!File.Exists(path))
        {
            Dalamud.Chat.PrintError("No file found with observed data at " + path);
            return;
        }
        //Dalamud.Chat.Print("Starting to read file at " + path);
        

        try
        {
            string[] fileInfo = File.ReadAllLines(path);
            
            for (int c = 0; c <= 11; c += 3)
            {
                if (c == 3)
                    c--;
                List<int> numCrafted = new List<int>();
                for (int row = 0; row < fileInfo.Length; row++)
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
                    //Dalamud.Chat.Print("Parsing column " + c+" d1: "+data1+" d2: "+data2+" d3: "+data3 +" summary: "+parseAsSummary);


                    if (row == Solver.items.Count) //Summary row
                    {
                        if(c> 0)
                        {
                            int.TryParse(data1, out int groove);
                            int.TryParse(data2, out int gross);
                            int.TryParse(data3, out int net);
                            endDays.Add(new EndDaySummary(groove, gross, net, numCrafted));
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
                            Enum.TryParse(data1, out Supply supp1);
                            Enum.TryParse(data2, out DemandShift demand1);
                            ObservedSupply d1 = new ObservedSupply(supp1, demand1);
                            observedSupplies.Add(new List<ObservedSupply>());
                            observedSupplies[row].Add(d1);
                            int.TryParse(data3, out int crafted1);
                            numCrafted.Add(crafted1);
                            break;
                        case 5:
                            Enum.TryParse(data1, out Supply supp2);
                            Enum.TryParse(data2, out DemandShift demand2);
                            ObservedSupply d2 = new ObservedSupply(supp2, demand2);
                            observedSupplies[row].Add(d2);
                            int.TryParse(data3, out int crafted2);
                            numCrafted.Add(crafted2);
                            break;
                        case 8:
                            Enum.TryParse(data1, out Supply supp3);
                            Enum.TryParse(data2, out DemandShift demand3);
                            observedSupplies[row].Add(new ObservedSupply(supp3, demand3));
                            int.TryParse(data3, out int crafted3);
                            numCrafted.Add(crafted3);
                            break;
                        case 11:
                            Enum.TryParse(data1, out Supply supp4);
                            Enum.TryParse(data2, out DemandShift demand4);
                            observedSupplies[row].Add(new ObservedSupply(supp4, demand4));
                            data3 = data3.Replace(" ", "");
                            Enum.TryParse(data3, out PeakCycle peak);
                            currentPeaks[row] = peak;
                            break;
                    }
                }
            }

            for(int c=14; c<21; c++)
            {
                string line = fileInfo[Solver.items.Count];
                string[] values = line.Split(",");
                if (values.Length <= c) //This can happen with summary rows
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

                if (c > 0)
                {
                    int.TryParse(data1, out int groove);
                    int.TryParse(data2, out int gross);
                    int.TryParse(data3, out int net);
                    endDays.Add(new EndDaySummary(groove, gross, net, new List<int>()));
                }
            }
        }
        catch (Exception e)
        {
            Dalamud.Chat.Print("Error importing csv " + path + ": " + e.Message);
        }
    }

}
