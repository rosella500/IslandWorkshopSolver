using System.Collections.Generic;
using System.Linq;

namespace Beachcomber.Solver;

public class SuggestedSchedules
{
    public IOrderedEnumerable<KeyValuePair<WorkshopSchedule, int>> orderedSuggestions { get; private set; }
    public SuggestedSchedules(Dictionary<WorkshopSchedule, int> suggestions, bool removeCantMake, Dictionary<int, int> inventory)
    {
        if (removeCantMake)
            RemoveSchedulesWeCantMake(suggestions, inventory);

        orderedSuggestions = suggestions.OrderByDescending(kvp => kvp.Value);
    }

    private static void RemoveSchedulesWeCantMake(Dictionary<WorkshopSchedule, int> schedules, Dictionary<int, int> inventory)
    {
        HashSet<WorkshopSchedule> schedulesToRemove = new HashSet<WorkshopSchedule>();
        foreach (var schedule in schedules.Keys)
        {
            Dictionary<Material, int> mats = new Dictionary<Material, int>();
            Solver.GetAllMatsForSchedule(schedule, mats);
            foreach (var mat in mats)
            {
                if (RareMaterialHelper.GetMaterialValue(mat.Key, out _) && (!inventory.ContainsKey((int)mat.Key) || inventory[(int)mat.Key] < mat.Value))
                {
                    schedulesToRemove.Add(schedule);
                    break;
                }
            }
        }

        foreach (var key in schedulesToRemove)
            schedules.Remove(key);
    }
}
