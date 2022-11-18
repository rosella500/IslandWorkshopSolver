using System.Collections.Generic;
using System.Linq;

namespace Beachcomber.Solver;

public class SuggestedSchedules
{
    public IOrderedEnumerable<KeyValuePair<WorkshopSchedule, int>> orderedSuggestions { get; private set; }
    public SuggestedSchedules(Dictionary<WorkshopSchedule, int> suggestions)
    {
        orderedSuggestions = suggestions.OrderByDescending(kvp => kvp.Value);
    }
}
