using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public sealed record WorkspaceTaskUnarchive([property: JsonRequired] long ArchivedRevision,
    WorkspaceTaskPlanning? Planning = null, WorkspaceTaskRelationships? Relationships = null)
{
    public bool IsValid() => ArchivedRevision is >= 1 and <= WorkspaceDocument.MaxRevision
        && (Planning is null || Planning.IsValid() && Planning.PlannedDate is null && !Planning.ClearPlannedDate) && (Relationships is null || Relationships.IsValid());
    public CompanionTask Apply(CompanionTask task) => (Relationships?.Apply(Planning?.Apply(task) ?? task) ?? Planning?.Apply(task) ?? task) with { IsArchived = false };
    public bool ReferencesAvailable(WorkspaceDocument document, CompanionTask task)
    {
        var restored = Apply(task);
        bool Has(Guid? id, IEnumerable<WorkspaceEntity> rows) => id is null || rows.Any(row => row.Id == id && !row.IsArchived);
        return (Relationships is null || Relationships.ReferencesAvailable(document, task))
            && Has(restored.CategoryId, document.Categories) && Has(restored.ProjectId, document.Projects)
            && Has(restored.SectionId, document.Sections) && (restored.SectionId is null || document.Sections.Any(row => row.Id == restored.SectionId && row.ProjectId == restored.ProjectId))
            && (restored.LabelIds ?? []).All(id => Has(id, document.Labels))
            && Has(restored.ParentTaskId, document.Tasks) && (restored.DependsOn ?? []).All(id => Has(id, document.Tasks));
    }
}
public sealed record WorkspaceOccurrenceCorrection([property: JsonRequired] Guid OccurrenceId,
    [property: JsonRequired] long OccurrenceRevision, [property: JsonRequired] Guid TargetCommandId)
{
    public bool IsValid() => OccurrenceId != Guid.Empty && TargetCommandId != Guid.Empty && OccurrenceRevision is >= 1 and <= WorkspaceDocument.MaxRevision;
}
public sealed record TaskUnarchive(Guid Id, long Revision, Guid TaskId, long ArchivedRevision, Guid CommandId, DateTimeOffset UnarchivedAtUtc, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record OccurrenceCorrection(Guid Id, long Revision, Guid TaskId, Guid CommandId, Guid TargetCommandId,
    TaskOccurrence PreviousOccurrence, DateTimeOffset CorrectedAtUtc, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);

public static class WorkspaceTaskRecovery
{
    public static bool HasGameFence(WorkspaceDocument document, Guid taskId) => document.Projections.Any(row => row.TaskId == taskId && (row.State != ProjectionState.Unlinked || row.HabiticaTaskId is not null));
    public static bool IsCorrected(WorkspaceDocument document, CompletionEvent completion) => document.OccurrenceCorrections?.Any(row => row.TargetCommandId == completion.CommandId
        && row.PreviousOccurrence is {} previous && previous.Id == completion.OccurrenceId && previous.Revision == completion.Revision) == true;
    public static bool ValidHistory(WorkspaceDocument document)
    {
        var commands = document.Completions.Select(row => row.CommandId).Concat(document.HabitScores.Select(row => row.CommandId))
            .Concat((document.Restorations ?? []).Select(row => row.CommandId)).Concat((document.ReviewEvents ?? []).Select(row => row.CommandId)).ToHashSet();
        var revisions = document.Completions.Select(row => row.Revision).Concat(document.HabitScores.Select(row => row.Revision))
            .Concat((document.Restorations ?? []).Select(row => row.Revision)).Concat((document.ReviewEvents ?? []).Select(row => row.Revision)).ToHashSet();
        var archives = new HashSet<(Guid, long)>();
        foreach (var row in document.Unarchives ?? [])
            if (row.IsArchived || row.CommandId == Guid.Empty || !commands.Add(row.CommandId) || !revisions.Add(row.Revision)
                || !archives.Add((row.TaskId, row.ArchivedRevision)) || row.ArchivedRevision < 1 || row.ArchivedRevision >= row.Revision || row.UnarchivedAtUtc == default || row.UnarchivedAtUtc.Offset != TimeSpan.Zero
                || !document.Tasks.Any(task => task.Id == row.TaskId && task.Revision >= row.Revision)) return false;
        var targets = new HashSet<Guid>();
        foreach (var row in document.OccurrenceCorrections ?? [])
        {
            var previous = row.PreviousOccurrence;
            var current = document.Occurrences.FirstOrDefault(value => value.Id == previous?.Id);
            if (row.IsArchived || previous is null || previous.IsArchived || previous.Id == Guid.Empty || previous.TaskId != row.TaskId
                || previous.TerminalCommandId is {} terminal && terminal != row.TargetCommandId
                || previous.Date == default || previous.State is not (OccurrenceState.Completed or OccurrenceState.Skipped)
                || previous.Revision < 1 || previous.Revision >= row.Revision || row.CommandId == Guid.Empty || row.TargetCommandId == Guid.Empty
                || row.CommandId == row.TargetCommandId || !commands.Add(row.CommandId) || !targets.Add(row.TargetCommandId) || !revisions.Add(row.Revision)
                || row.CorrectedAtUtc == default || row.CorrectedAtUtc.Offset != TimeSpan.Zero || current is null || current.TaskId != row.TaskId || current.Date != previous.Date
                || current.Revision < row.Revision || !document.Tasks.Any(task => task.Id == row.TaskId && task.Kind == CompanionTaskKind.Daily && task.Revision >= row.Revision)) return false;
            if (previous.State == OccurrenceState.Completed
                ? !document.Completions.Any(value => value.TaskId == row.TaskId && value.OccurrenceId == previous.Id && value.Revision == previous.Revision && value.CommandId == row.TargetCommandId)
                : document.Completions.Any(value => value.CommandId == row.TargetCommandId)) return false;
        }
        foreach (var group in (document.OccurrenceCorrections ?? []).GroupBy(row => row.PreviousOccurrence.Id))
        {
            var rows = group.OrderBy(row => row.Revision).ToArray();
            for (var index = 1; index < rows.Length; index++) if (rows[index].PreviousOccurrence.Revision <= rows[index - 1].Revision) return false;
            var current = document.Occurrences.Single(row => row.Id == group.Key);
            if ((current.State == OccurrenceState.Pending) != (current.Revision == rows[^1].Revision)) return false;
        }
        return true;
    }
}
