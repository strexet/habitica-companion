using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceRecurrenceCommandsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-17T00:00:00Z");
    private static RecurrenceRule Schedule => new(RecurrenceFrequency.Weekly, 1, new(2026, 10, 17), "UTC", [DayOfWeek.Monday], []);
    private static WorkspaceJournalSnapshot Empty() => new(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, []);
    private static CommandSubmission Apply(WorkspaceJournalSnapshot state, WorkspaceOperation operation, Guid id, WorkspaceTaskPatch? patch = null, DateOnly? date = null, bool project = false) => WorkspaceCommandEngine.Accept(state, new(Guid.NewGuid(), state.Document.Id, state.Document.Revision, operation, id, patch, OccurrenceDate: date, ProjectToHabitica: project), Now);

    [Fact]
    public void Reviewed_schedule_survives_partial_edit_completion_clear_and_archive_roundtrip()
    {
        var state = Empty(); var id = Guid.NewGuid();
        var created = Apply(state, WorkspaceOperation.CreateDaily, id, new(Text: "Recurring reading", Recurrence: Schedule));
        Assert.Equal(SubmissionDisposition.Accepted, created.Disposition); Assert.Equal(ProjectionCommandState.NotRequested, created.Entry!.ProjectionState); Assert.Empty(created.Entry.Effects);
        state = WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(created.Snapshot!));
        var partial = Apply(state, WorkspaceOperation.UpdateTask, id, new(Notes: "Keep schedule"));
        Assert.Equal(Schedule.Frequency, partial.Snapshot!.Document.Tasks[0].Recurrence!.Frequency);
        Assert.Equal(Schedule.Weekdays, partial.Snapshot.Document.Tasks[0].Recurrence!.Weekdays);
        state = partial.Snapshot;
        Assert.Equal("occurrence_not_scheduled", Apply(state, WorkspaceOperation.CompleteTask, id, date: new(2026, 10, 18)).ErrorCode);
        var complete = Apply(state, WorkspaceOperation.CompleteTask, id, date: new(2026, 10, 19));
        Assert.Equal(SubmissionDisposition.Accepted, complete.Disposition); state = complete.Snapshot!;
        var duplicate = WorkspaceCommandEngine.Accept(state, complete.Entry!.Command, Now.AddMinutes(1));
        Assert.Equal(SubmissionDisposition.Duplicate, duplicate.Disposition); Assert.Single(duplicate.Snapshot!.Document.Completions);
        var cleared = Apply(state, WorkspaceOperation.UpdateTask, id, new(ClearRecurrence: true));
        Assert.Equal(SubmissionDisposition.Accepted, cleared.Disposition); Assert.Null(cleared.Snapshot!.Document.Tasks[0].Recurrence);
        Assert.Single(cleared.Snapshot.Document.Completions); Assert.Single(cleared.Snapshot.Document.Occurrences);
        Assert.NotNull(cleared.Snapshot.Commands[0].TaskSnapshot!.Recurrence);
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(cleared.Snapshot))));
        Assert.Equal(SubmissionDisposition.Accepted, Apply(cleared.Snapshot, WorkspaceOperation.CompleteTask, id, date: new(2026, 10, 20)).Disposition);
    }

    [Fact]
    public void Invalid_rules_wrong_kinds_and_projection_requests_never_accept_schedule_edits()
    {
        var state = Empty(); var id = Guid.NewGuid();
        foreach (var operation in new[] { WorkspaceOperation.CreateTodo, WorkspaceOperation.CreateHabit })
            Assert.Equal(SubmissionDisposition.Invalid, Apply(state, operation, id, new(Text: "Reject", Recurrence: Schedule)).Disposition);
        foreach (var rule in new[] { Schedule with { Interval = 0 }, Schedule with { TimeZoneId = "Missing/Zone" }, Schedule with { Weekdays = [] } })
            Assert.Equal(SubmissionDisposition.Invalid, Apply(state, WorkspaceOperation.CreateDaily, id, new(Text: "Reject", Recurrence: rule)).Disposition);
        Assert.Equal(SubmissionDisposition.Invalid, Apply(state, WorkspaceOperation.CreateDaily, id, new(Text: "Reject", Recurrence: Schedule, ClearRecurrence: true)).Disposition);
        Assert.Equal(SubmissionDisposition.Accepted, Apply(state, WorkspaceOperation.CreateDaily, id, new(Text: "Supported", Recurrence: Schedule), project: true).Disposition);
        state = Apply(state, WorkspaceOperation.CreateDaily, id, new(Text: "Unlinked")).Snapshot!;
        foreach (var projection in new[] { ProjectionState.Linked, ProjectionState.Pending, ProjectionState.UnknownOutcome, ProjectionState.ExternalConflict })
        {
            var mapped = state with { Document = state.Document with { Projections = [new(Guid.NewGuid(), state.Document.Revision, id, projection, projection == ProjectionState.Linked ? Guid.NewGuid().ToString("D") : null)] } };
            Assert.Equal("unsupported_projection", Apply(mapped, WorkspaceOperation.UpdateTask, id, new(Recurrence: Schedule)).ErrorCode);
            Assert.Equal("unsupported_projection", Apply(mapped, WorkspaceOperation.UpdateTask, id, new(ClearRecurrence: true)).ErrorCode);
        }
    }

    [Fact]
    public void Legacy_request_defaults_remain_absent_and_extended_intent_cannot_import_game_effects()
    {
        using var json = JsonDocument.Parse(WorkspaceTaskApiJson.Serialize(new WorkspaceTaskPatch(Text: "Legacy")));
        Assert.False(json.RootElement.TryGetProperty("recurrence", out _)); Assert.False(json.RootElement.TryGetProperty("clearRecurrence", out _));
        var state = Empty(); var created = Apply(state, WorkspaceOperation.CreateDaily, Guid.NewGuid(), new(Text: "Canonical only", Recurrence: Schedule)).Snapshot!;
        var entry = created.Commands[0];
        var command = entry.Command with { ProjectToHabitica = true };
        var projected = created with { Commands = [entry with { Command = command, PayloadHash = WorkspaceCommandIdentity.Hash(command), Effects = [ProjectionEffectKind.Create], ProjectionState = ProjectionCommandState.Pending }] };
        Assert.False(WorkspaceJournalValidation.IsValid(projected)); // Existing coverage binds the original command hash.
        Assert.True(WorkspaceJournalValidation.IsValid(projected with { Document = projected.Document with { ActivityCoverage = null } }));
        var malformed = entry.Command with { Patch = entry.Command.Patch! with { Recurrence = Schedule with { Weekdays = null! } } };
        Assert.False(WorkspaceJournalValidation.IsValid(created with { Commands = [entry with { Command = malformed }] }));
        Assert.False(WorkspaceJournalValidation.IsValid(created with { Commands = [entry with { TaskSnapshot = null }] }));
        Assert.False(WorkspaceJournalValidation.IsValid(created with { Commands = [entry with { TaskSnapshot = entry.TaskSnapshot! with { Recurrence = Schedule with { Interval = 2 } } }] }));
    }
}
