using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Habitica.Domain.Workspace;

public static class WorkspaceCommandIdentity
{
    public static string Hash(WorkspaceCommand command)
    {
        static string? Name<T>(T? value) where T : struct, Enum => value is null ? null : JsonNamingPolicy.CamelCase.ConvertName(value.Value.ToString());
        var patch = command.Patch;
        if (patch?.Unarchive is not null || patch?.CorrectOccurrence is not null)
        {
            var previous = Hash(command with { Patch = patch with { Unarchive = null, CorrectOccurrence = null } });
            var unarchive = patch.Unarchive; var correction = patch.CorrectOccurrence;
            var recoveryFields = new string?[] { unarchive?.ArchivedRevision.ToString(CultureInfo.InvariantCulture),
                unarchive?.Planning is {} plan ? Hash(command with { Operation = WorkspaceOperation.PlanTask, Patch = new(Planning: plan) }) : null,
                unarchive?.Relationships is {} links ? Hash(command with { Operation = WorkspaceOperation.RelateTodo, Patch = new(Relationships: links) }) : null,
                correction?.OccurrenceId.ToString("D"), correction?.OccurrenceRevision.ToString(CultureInfo.InvariantCulture), correction?.TargetCommandId.ToString("D") };
            var payload = "companion-command-v11\n64:" + previous + string.Concat(recoveryFields.Select(field => field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }
        if (patch?.Review is {} review)
        {
            var previous = Hash(command with { Patch = patch with { Review = null } });
            var payload = "companion-command-v10\n64:" + previous;
            foreach (var field in new[] { Name<TaskReviewAction>(review.Action), review.TimeZoneId, WorkspaceTaskReview.CanonicalUtc(review.ReviewAfterUtc), WorkspaceTaskReview.CanonicalUtc(review.StaleSinceUtc) })
                payload += field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field;
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }
        if (patch?.HabiticaDetails is { } directions && (directions.Up is not null || directions.Down is not null))
        {
            var previous = Hash(command with { Patch = patch with { HabiticaDetails = directions with { Up = null, Down = null } } });
            var payload = "companion-command-v9\n64:" + previous;
            foreach (var flag in new[] { directions.Up, directions.Down }) payload += flag is null ? "-1:" : flag.Value ? "1:1" : "1:0";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }

        var fields = new string?[]
        {
            command.Id.ToString("D"), command.WorkspaceId.ToString("D"), command.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
            Name<WorkspaceOperation>(command.Operation), command.TaskId.ToString("D"), patch is null ? "0" : "1",
            patch?.Text, patch?.Notes, patch?.ClearNotes == true ? "1" : "0", Name(patch?.Priority),
            patch?.Deadline?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), patch?.ClearDeadline == true ? "1" : "0",
            Name(command.Direction), command.Count.ToString(CultureInfo.InvariantCulture), command.OccurrenceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            command.ProjectToHabitica ? "1" : "0"
        };
        var details = patch?.HabiticaDetails;
        var extended = patch?.Recurrence is not null || patch?.ClearRecurrence == true;
        var planning = patch?.Planning;
        var restore = patch?.RestoreTodo;
        var relationships = patch?.Relationships;
        var todoState = patch?.TodoState;
        var todoStage = patch?.TodoStage;
        var body = new StringBuilder(todoStage is not null ? "companion-command-v8\n" : todoState is not null ? "companion-command-v7\n" : relationships is not null ? "companion-command-v6\n" : restore is not null ? "companion-command-v5\n" : planning is not null ? "companion-command-v4\n" : extended ? "companion-command-v3\n" : details is null ? "companion-command-v1\n" : "companion-command-v2\n");
        foreach (var field in fields) body.Append(field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field);
        if (extended || planning is not null || restore is not null || relationships is not null || todoState is not null || todoStage is not null) body.Append(details is null ? "1:0" : "1:1");
        if (details is not null)
        {
            var extra = new List<string?> { details.Difficulty?.ToString("R", CultureInfo.InvariantCulture), Name(details.Attribute), details.Checklist?.Count.ToString(CultureInfo.InvariantCulture) };
            foreach (var item in details.Checklist ?? Array.Empty<TaskChecklistItem>()) { extra.Add(item.Id.ToString("D")); extra.Add(item.Text); extra.Add(item.Completed ? "1" : "0"); }
            foreach (var field in extra) body.Append(field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field);
        }
        if (extended || planning is not null || restore is not null || relationships is not null || todoState is not null || todoStage is not null)
        {
            var rule = patch?.Recurrence;
            var extra = new List<string?> { patch?.ClearRecurrence == true ? "1" : "0", rule is null ? "0" : "1",
                Name(rule?.Frequency), rule?.Interval.ToString(CultureInfo.InvariantCulture), rule?.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                rule?.TimeZoneId, rule?.Weekdays?.Count.ToString(CultureInfo.InvariantCulture), rule?.DaysOfMonth?.Count.ToString(CultureInfo.InvariantCulture) };
            foreach (var day in rule?.Weekdays ?? Array.Empty<DayOfWeek>()) extra.Add(Name<DayOfWeek>(day));
            foreach (var day in rule?.DaysOfMonth ?? Array.Empty<int>()) extra.Add(day.ToString(CultureInfo.InvariantCulture));
            foreach (var field in extra) body.Append(field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field);
        }
        if (restore is not null || relationships is not null || todoState is not null || todoStage is not null) body.Append(planning is null ? "1:0" : "1:1");
        if (planning is not null)
        {
            var extra = new List<string?> { planning.PlannedDate?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture), planning.ClearPlannedDate ? "1" : "0",
                planning.CategoryId?.ToString("D"),planning.ClearCategory ? "1" : "0",planning.ProjectId?.ToString("D"),planning.ClearProject ? "1" : "0",
                planning.SectionId?.ToString("D"),planning.ClearSection ? "1" : "0",planning.LabelIds?.Count.ToString(CultureInfo.InvariantCulture) };
            foreach (var id in planning.LabelIds ?? Array.Empty<Guid>()) extra.Add(id.ToString("D"));
            foreach (var field in extra) body.Append(field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field);
        }
        if (relationships is not null || todoState is not null || todoStage is not null) body.Append(restore is null ? "1:0" : "1:1");
        if (restore is not null)
        {
            var id = restore.CompletionId.ToString("D"); body.Append(Encoding.UTF8.GetByteCount(id).ToString(CultureInfo.InvariantCulture)).Append(':').Append(id);
        }
        if (relationships is not null)
        {
            var extra = new List<string?> { relationships.ParentTaskId?.ToString("D"),relationships.ClearParentTask ? "1" : "0",relationships.DependsOn?.Count.ToString(CultureInfo.InvariantCulture) };
            foreach (var id in relationships.DependsOn ?? Array.Empty<Guid>()) extra.Add(id.ToString("D"));
            foreach (var field in extra) body.Append(field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field);
        }
        if (todoState is not null || todoStage is not null) body.Append(relationships is null ? "1:0" : "1:1");
        if (todoState is not null)
        {
            var state = Name<CompanionTaskState>(todoState.State)!;body.Append(Encoding.UTF8.GetByteCount(state).ToString(CultureInfo.InvariantCulture)).Append(':').Append(state);
        }
        if (todoStage is not null)
        {
            body.Append(todoState is null ? "1:0" : "1:1");
            var stage=Name(todoStage.Stage);body.Append(stage is null ? "-1:" : Encoding.UTF8.GetByteCount(stage).ToString(CultureInfo.InvariantCulture)+":"+stage).Append(todoStage.ClearStage?"1:1":"1:0");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.ToString()))).ToLowerInvariant();
    }

}
