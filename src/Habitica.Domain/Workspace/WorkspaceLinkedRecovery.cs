using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public sealed record CompletionScoreSource(
    [property: JsonRequired] string Contract,
    [property: JsonRequired] Guid WorkspaceId,
    [property: JsonRequired] Guid TaskId,
    [property: JsonRequired] Guid CommandId,
    [property: JsonRequired] string CommandPayloadHash,
    [property: JsonRequired] Guid CompletionId,
    [property: JsonRequired] int EffectIndex,
    [property: JsonRequired] CompanionTaskKind TaskKind,
    DateOnly? OccurrenceDate = null)
{
    public bool IsValid() => Contract == "companion-completion-score-v1" && WorkspaceId != Guid.Empty && TaskId != Guid.Empty
        && CommandId != Guid.Empty && CompletionId != Guid.Empty && WorkspaceLinkedRecovery.ValidHash(CommandPayloadHash)
        && EffectIndex is >= 0 and <= 10 && TaskKind is CompanionTaskKind.Todo or CompanionTaskKind.Daily
        && (TaskKind == CompanionTaskKind.Todo ? OccurrenceDate is null : OccurrenceDate is not null && OccurrenceDate != DateOnly.MinValue);
}
public sealed record LinkedTaskRecovery(Guid Id, long Revision, Guid TaskId, Guid CompletionId, Guid SourceAttemptId,
    Guid RecoveryAttemptId, Guid HabiticaTaskId, Guid AccountId, long ReviewedRevision, long TaskRevision,
    Guid MappingId, long MappingRevision, string MappingFingerprint, string ContextHash, string UpstreamReviewHash,
    CompletionScoreSource SourceContext, DateTimeOffset RecoveredAtUtc, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);

public static class WorkspaceLinkedRecovery
{
    public static bool ValidHash(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static string ContextHash(Guid workspaceId, LinkedTaskRecovery row, CompletionEvent completion)
    {
        var source = row.SourceContext;
        var fields = new string?[] { workspaceId.ToString("D"), row.ReviewedRevision.ToString(CultureInfo.InvariantCulture), row.TaskId.ToString("D"), row.TaskRevision.ToString(CultureInfo.InvariantCulture),
            completion.Id.ToString("D"), source.CommandId.ToString("D"), source.CommandPayloadHash, source.EffectIndex.ToString(CultureInfo.InvariantCulture), row.SourceAttemptId.ToString("D"),
            row.HabiticaTaskId.ToString("D"), row.MappingId.ToString("D"), row.MappingRevision.ToString(CultureInfo.InvariantCulture), row.MappingFingerprint, row.AccountId.ToString("D"),
            source.TaskKind == CompanionTaskKind.Todo ? "todo" : "daily", completion.OccurrenceId?.ToString("D"), source.OccurrenceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            (completion.CompletedAtUtc.UtcDateTime.Ticks - DateTime.UnixEpoch.Ticks).ToString(CultureInfo.InvariantCulture) };
        var payload = "companion-linked-recovery-context-v1\n" + string.Concat(fields.Select(field => field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
    public static bool ValidHistory(WorkspaceDocument document)
    {
        var completed = new HashSet<Guid>();
        if (document.LinkedTaskRecoveries?.Count > 1000) return false;
        foreach (var row in document.LinkedTaskRecoveries ?? [])
        {
            if (row.SourceContext is not {} source || !source.IsValid() || row.IsArchived || !completed.Add(row.CompletionId)
                || row.TaskId == Guid.Empty || row.CompletionId == Guid.Empty || row.SourceAttemptId == Guid.Empty || row.RecoveryAttemptId != row.Id || row.SourceAttemptId == row.RecoveryAttemptId || row.Id == source.CommandId
                || row.HabiticaTaskId == Guid.Empty || row.AccountId == Guid.Empty || row.MappingId == Guid.Empty
                || row.ReviewedRevision < 1 || row.ReviewedRevision + 1 != row.Revision || row.TaskRevision < 1 || row.TaskRevision > row.ReviewedRevision || row.MappingRevision < 1 || row.MappingRevision > row.ReviewedRevision
                || !ValidHash(row.MappingFingerprint) || !ValidHash(row.ContextHash) || !ValidHash(row.UpstreamReviewHash)
                || source.WorkspaceId != document.Id || source.TaskId != row.TaskId || source.CompletionId != row.CompletionId
                || row.RecoveredAtUtc == default || row.RecoveredAtUtc.Offset != TimeSpan.Zero) return false;
            var completion = document.Completions.FirstOrDefault(value => value.Id == row.CompletionId);
            var task = document.Tasks.FirstOrDefault(value => value.Id == row.TaskId);
            if (completion is null || task is null || completion.TaskId != row.TaskId || completion.CommandId != source.CommandId || task.Kind != source.TaskKind
                || task.Revision < row.Revision || completion.Revision > row.ReviewedRevision || row.RecoveredAtUtc < completion.CompletedAtUtc || ContextHash(document.Id, row, completion) != row.ContextHash) return false;
            if (source.TaskKind == CompanionTaskKind.Todo)
            {
                var native = (document.Restorations ?? []).Where(value => value.CommandId == row.Id).ToArray();
                if (native.Length != 1 || native[0].TaskId != row.TaskId || native[0].CompletionId != row.CompletionId || native[0].Revision != row.Revision || native[0].RestoredAtUtc != row.RecoveredAtUtc) return false;
            }
            else
            {
                var native = (document.OccurrenceCorrections ?? []).Where(value => value.CommandId == row.Id).ToArray();
                if (native.Length != 1 || native[0].PreviousOccurrence is not {} previous || native[0].TaskId != row.TaskId || native[0].TargetCommandId != source.CommandId || native[0].Revision != row.Revision
                    || previous.Id != completion.OccurrenceId || previous.Revision != completion.Revision || previous.Date != source.OccurrenceDate || previous.State != OccurrenceState.Completed || native[0].CorrectedAtUtc != row.RecoveredAtUtc) return false;
            }
        }
        return true;
    }
    public static bool ValidJournal(WorkspaceJournalSnapshot snapshot)
    {
        foreach(var restoration in snapshot.Document.Restorations ?? [])
        {
            var completion=snapshot.Document.Completions.FirstOrDefault(value=>value.Id==restoration.CompletionId);
            var source=snapshot.Commands.FirstOrDefault(value=>value.Command.Id==completion?.CommandId);
            if(source is {Effects.Count:>0} && !(snapshot.Document.LinkedTaskRecoveries ?? []).Any(value=>value.Id==restoration.CommandId&&value.CompletionId==restoration.CompletionId))return false;
        }
        foreach(var correction in snapshot.Document.OccurrenceCorrections ?? [])
        {
            var source=snapshot.Commands.FirstOrDefault(value=>value.Command.Id==correction.TargetCommandId);
            if(source is null || source.Command.TaskId!=correction.TaskId || source.Command.ExpectedRevision+1!=correction.PreviousOccurrence.Revision
                || source.Command.OccurrenceDate!=correction.PreviousOccurrence.Date || source.Command.Operation!=(correction.PreviousOccurrence.State==OccurrenceState.Completed?WorkspaceOperation.CompleteTask:WorkspaceOperation.SkipOccurrence)
                || source.Effects.Count>0 && !(snapshot.Document.LinkedTaskRecoveries ?? []).Any(value=>value.Id==correction.CommandId&&value.SourceContext.CommandId==correction.TargetCommandId))return false;
        }
        foreach (var row in snapshot.Document.LinkedTaskRecoveries ?? [])
        {
            var source = snapshot.Commands.FirstOrDefault(value => value.Command.Id == row.SourceContext.CommandId);
            if (source is null || source.Command.Operation != WorkspaceOperation.CompleteTask || source.Command.TaskId != row.TaskId || source.PayloadHash != row.SourceContext.CommandPayloadHash
                || source.ProjectionState != ProjectionCommandState.Confirmed || source.ConfirmedEffects != source.Effects.Count || source.Effects.ElementAtOrDefault(row.SourceContext.EffectIndex) != ProjectionEffectKind.ScoreUp
                || !(snapshot.Events ?? []).Any(value => value.CommandId == source.Command.Id && value.Kind == JournalEventKind.Dispatched && value.ConfirmedEffects == row.SourceContext.EffectIndex && value.AttemptToken == row.SourceAttemptId)) return false;
        }
        return true;
    }
}
