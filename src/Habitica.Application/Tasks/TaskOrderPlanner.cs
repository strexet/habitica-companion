namespace Habitica.Application.Tasks;

public enum TaskDropPlacement
{
    Before,
    After
}

public sealed class TaskOrderPlanner
{
    public IReadOnlyList<string> ReorderVisibleSubset(
        IEnumerable<string> allOrderedTaskIds,
        IEnumerable<string> visibleTaskIds,
        string draggedTaskId,
        string targetTaskId,
        TaskDropPlacement placement)
    {
        var allOrderedIds = Normalize(allOrderedTaskIds).ToArray();
        var visibleIds = Normalize(visibleTaskIds).ToList();

        if (string.Equals(draggedTaskId, targetTaskId, StringComparison.Ordinal)
            || !visibleIds.Contains(draggedTaskId, StringComparer.Ordinal)
            || !visibleIds.Contains(targetTaskId, StringComparer.Ordinal))
        {
            return allOrderedIds;
        }

        visibleIds.RemoveAll(id => string.Equals(id, draggedTaskId, StringComparison.Ordinal));
        var targetIndex = visibleIds.FindIndex(id => string.Equals(id, targetTaskId, StringComparison.Ordinal));
        if (targetIndex < 0)
        {
            return allOrderedIds;
        }

        var insertIndex = placement == TaskDropPlacement.After ? targetIndex + 1 : targetIndex;
        visibleIds.Insert(insertIndex, draggedTaskId);

        return MergeVisibleOrder(allOrderedIds, visibleIds);
    }

    public IReadOnlyList<string> MoveVisibleItem(
        IEnumerable<string> allOrderedTaskIds,
        IEnumerable<string> visibleTaskIds,
        string taskId,
        int direction)
    {
        var allOrderedIds = Normalize(allOrderedTaskIds).ToArray();
        var visibleIds = Normalize(visibleTaskIds).ToList();
        var index = visibleIds.FindIndex(id => string.Equals(id, taskId, StringComparison.Ordinal));
        var targetIndex = index + direction;
        if (index < 0 || targetIndex < 0 || targetIndex >= visibleIds.Count)
        {
            return allOrderedIds;
        }

        (visibleIds[index], visibleIds[targetIndex]) = (visibleIds[targetIndex], visibleIds[index]);
        return MergeVisibleOrder(allOrderedIds, visibleIds);
    }

    private static IReadOnlyList<string> MergeVisibleOrder(IEnumerable<string> allOrderedTaskIds, IReadOnlyCollection<string> reorderedVisibleIds)
    {
        var visibleQueue = new Queue<string>(reorderedVisibleIds);
        var visibleIdSet = reorderedVisibleIds.ToHashSet(StringComparer.Ordinal);
        var nextOrder = new List<string>();

        foreach (var taskId in Normalize(allOrderedTaskIds))
        {
            nextOrder.Add(visibleIdSet.Contains(taskId) && visibleQueue.Count > 0 ? visibleQueue.Dequeue() : taskId);
        }

        while (visibleQueue.Count > 0)
        {
            nextOrder.Add(visibleQueue.Dequeue());
        }

        return nextOrder.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> Normalize(IEnumerable<string> taskIds)
    {
        return taskIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal);
    }
}
