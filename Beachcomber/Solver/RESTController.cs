using Dalamud.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Beachcomber.Solver;
using static PeakCycle;
public static class RESTController
{
    private static readonly HttpClient Client = new HttpClient();

    public static async Task SendPeakData()
    {
        Dictionary<string, string> postData = new Dictionary<string, string>();
        postData.Add("week", Solver.Week.ToString());
        postData.Add("day", Solver.CurrentDay.ToString());

        StringBuilder peaks = new StringBuilder();
        foreach (ItemInfo item in Solver.Items)
        {
            switch (item.peak)
            {
                case Unknown:
                    peaks.Append("U");
                    break;
                case Cycle2Weak:
                    peaks.Append("2W");
                    break;
                case Cycle2Strong:
                    peaks.Append("2S");
                    break;
                case Cycle3Weak:
                    peaks.Append("3W");
                    break;
                case Cycle3Strong:
                    peaks.Append("3S");
                    break;
                case Cycle4Weak:
                    peaks.Append("4W");
                    break;
                case Cycle4Strong:
                    peaks.Append("4S");
                    break;
                case Cycle5Weak:
                    peaks.Append("5W");
                    break;
                case Cycle5Strong:
                    peaks.Append("5S");
                    break;
                case Cycle6Weak:
                    peaks.Append("6W");
                    break;
                case Cycle6Strong:
                    peaks.Append("6S");
                    break;
                case Cycle7Weak:
                    peaks.Append("7W");
                    break;
                case Cycle7Strong:
                    peaks.Append("7S");
                    break;
                case Cycle45:
                    peaks.Append("45");
                    break;
                case Cycle5:
                    peaks.Append("5U");
                    break;
                case Cycle67:
                    peaks.Append("67");
                    break;
                case Cycle2:
                    peaks.Append("2U");
                    break;
                case UnknownD1:
                    peaks.Append("U1");
                    break;
            }
            if (item != Solver.Items[Solver.Items.Count - 1])
            {
                peaks.Append(',');
            }
        }

        postData.Add("peaks", peaks.ToString());

        await SendPost("peaks", postData);
    }

    private static async Task SendPost(string endpoint, Dictionary<string, string> values)
    {
        var content = new FormUrlEncodedContent(values);
        var response = await Client.PostAsync("http://island.ws:1483/"+endpoint, content);
        var responseString = await response.Content.ReadAsStringAsync();
        PluginLog.Warning("Response from /"+endpoint+": " + responseString);
    }
    public static async Task SendPopularityData()
    {
        var values = new Dictionary<string, string>();
        values.Add("week", Solver.Week.ToString());
        values.Add("pop", Reader.currentPopIndex.ToString());
        values.Add("nextPop", Reader.nextPopIndex.ToString());
        PluginLog.Debug("Sending new popularity data. Week {0}, currentPop {1}, nextPop {2}", Solver.Week, Reader.currentPopIndex, Reader.nextPopIndex);

        await SendPost("pop", values);
    }

    public static async Task ReadGetData()
    {
        var responseString = await Client.GetStringAsync("http://island.ws:1483/?week=" + Solver.Week);
        PluginLog.LogDebug("Response from get data: " + responseString);
        if (responseString.Contains("error"))
            PluginLog.Error(responseString);

    }
}

