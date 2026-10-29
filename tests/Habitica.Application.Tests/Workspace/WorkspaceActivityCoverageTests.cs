using System.Text.Json.Nodes;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceActivityCoverageTests
{
    private static DateTimeOffset Time(int hour) => new(2026, 10, 27, hour, 0, 0, TimeSpan.Zero);
    private static RecurrenceRule Rule(string zone = "UTC") => new(RecurrenceFrequency.Daily, 1, new(2026, 10, 27), zone, [], []);
    private static WorkspaceJournalSnapshot Start(CompanionTaskKind kind = CompanionTaskKind.Daily)
    {
        var state = new WorkspaceJournalSnapshot(new() { Id = Guid.NewGuid() }, 1, [], []);
        return Apply(state, kind == CompanionTaskKind.Daily ? WorkspaceOperation.CreateDaily : kind == CompanionTaskKind.Habit ? WorkspaceOperation.CreateHabit : WorkspaceOperation.CreateTodo, 0,
            new(Text: "Synthetic coverage", Recurrence: kind == CompanionTaskKind.Daily ? Rule() : null));
    }
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot state, WorkspaceOperation operation, int hour, WorkspaceTaskPatch? patch = null, HabitScoreDirection? direction = null, int count = 1)
    {
        var command = new WorkspaceCommand(Guid.NewGuid(), state.Document.Id, state.Document.Revision, operation,
            state.Document.Tasks.FirstOrDefault()?.Id ?? Guid.NewGuid(), patch, direction, count,
            OccurrenceDate: operation is WorkspaceOperation.CompleteTask or WorkspaceOperation.SkipOccurrence && state.Document.Tasks[0].Kind == CompanionTaskKind.Daily ? new(2026, 10, 27) : null);
        var result = WorkspaceCommandEngine.Accept(state, command, Time(hour));
        Assert.Equal(SubmissionDisposition.Accepted, result.Disposition); Assert.Empty(result.Entry!.Effects);
        Assert.True(WorkspaceJournalValidation.IsValid(result.Snapshot!)); return result.Snapshot!;
    }
    private static IReadOnlyList<TaskActivityInterval> Read(WorkspaceJournalSnapshot state, int from = 0, int through = 3) => WorkspaceActivityCoverage.Read(state, state.Document.Tasks[0].Id, Time(from), Time(through));

    [Fact]
    public void Effective_schedule_timezone_pause_resume_and_unknown_tail_are_pure()
    {
        var state = Start(); state = Apply(state, WorkspaceOperation.UpdateTask, 1, new(Recurrence: Rule("Europe/Prague")));
        state = Apply(state, WorkspaceOperation.PauseTask, 2); state = Apply(state, WorkspaceOperation.ResumeTask, 3); state = Apply(state, WorkspaceOperation.UpdateTask, 4, new(Text: "tail"));
        var intervals = Read(state, through: 4); Assert.Equal(4, intervals.Count); Assert.All(intervals, row => Assert.True(row.IsKnown));
        Assert.Equal("UTC", intervals[0].Schedule!.Recurrence!.TimeZoneId); Assert.Equal("Europe/Prague", intervals[1].Schedule!.Recurrence!.TimeZoneId);
        Assert.Equal(CompanionTaskState.Paused, intervals[2].Schedule!.State); Assert.Equal(CompanionTaskState.Active, intervals[3].Schedule!.State);
        var before = WorkspaceJournalJson.Serialize(state); Assert.False(Read(state, through: 5)[^1].IsKnown); Assert.Equal(before, WorkspaceJournalJson.Serialize(state));
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(before)));
        Assert.Equal(state.Document.ActivityCoverage!.Count, WorkspaceJson.Deserialize(WorkspaceJson.Serialize(state.Document)).ActivityCoverage!.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Missing_witness_or_retained_revision_never_bridges_gap(bool removeCommand)
    {
        var state = Start(); for (var h = 1; h <= 3; h++) state = Apply(state, WorkspaceOperation.UpdateTask, h, new(Text: h.ToString()));
        var removed = state.Document.ActivityCoverage![1].CommandId;
        state = state with { Document = state.Document with { ActivityCoverage = state.Document.ActivityCoverage.Where(row => row.CommandId != removed).ToArray() },
            Commands = removeCommand ? state.Commands.Where(row => row.Command.Id != removed).ToArray() : state.Commands,
            Events = removeCommand ? state.Events!.Where(row => row.CommandId != removed).ToArray() : state.Events };
        Assert.True(WorkspaceJournalValidation.IsValid(state)); var intervals = Read(state);
        Assert.False(intervals[0].IsKnown); Assert.Equal(Time(2), intervals[0].ThroughUtc); Assert.True(intervals[1].IsKnown);
    }

    [Fact]
    public void Legacy_import_keeps_unknown_prefix_despite_creation_date_and_complete_looking_commands()
    {
        var state = Start(); state = Apply(state, WorkspaceOperation.UpdateTask, 1, new(Text: "legacy"));
        state = WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state with { Document = state.Document with { ActivityCoverage = null } }));
        Assert.All(Read(state), row => Assert.False(row.IsKnown)); state = Apply(state, WorkspaceOperation.UpdateTask, 2, new(Text: "first witness")); state = Apply(state, WorkspaceOperation.UpdateTask, 3, new(Text: "second witness"));
        var intervals = Read(state); Assert.False(intervals[0].IsKnown); Assert.Equal(Time(2), intervals[0].ThroughUtc); Assert.True(intervals[1].IsKnown);
    }

    [Theory]
    [InlineData(CompanionTaskKind.Todo)] [InlineData(CompanionTaskKind.Daily)] [InlineData(CompanionTaskKind.Habit)]
    public void Native_archive_and_recovery_preserve_identity_and_causal_schedule(CompanionTaskKind kind)
    {
        var state = Start(kind); if (kind == CompanionTaskKind.Todo) state = Apply(state, WorkspaceOperation.CompleteTask, 1);
        state = Apply(state, WorkspaceOperation.DeleteTask, 2); state = Apply(state, WorkspaceOperation.UnarchiveTask, 3, new(Unarchive: new(state.Document.Tasks[0].Revision)));
        state = kind == CompanionTaskKind.Todo ? Apply(state, WorkspaceOperation.RestoreTodo, 4, new(RestoreTodo: new(state.Document.Completions[0].Id))) : Apply(state, WorkspaceOperation.UpdateTask, 4, new(Text: "tail"));
        var intervals = Read(state, through: 4); Assert.All(intervals, row => Assert.True(row.IsKnown)); Assert.True(intervals.Single(row => row.FromUtc == Time(2)).Schedule!.IsArchived);
        Assert.Single(state.Document.Tasks); Assert.All(state.Commands, row => Assert.Empty(row.Effects));
    }

    [Fact]
    public void Causal_assertions_require_actual_retained_scores_and_accepted_events()
    {
        var state = Start(CompanionTaskKind.Habit); state = Apply(state, WorkspaceOperation.ScoreHabit, 1, direction: HabitScoreDirection.Up, count: 2);
        state = Apply(state, WorkspaceOperation.ScoreHabit, 2, direction: HabitScoreDirection.Down); state = Apply(state, WorkspaceOperation.UpdateTask, 3, new(Text: "tail"));
        Assert.All(Read(state), row => Assert.True(row.IsKnown));
        var corruptions = new Action<JsonObject>[] {
            s => s["document"]!["activityCoverage"]![1]!["payloadHash"] = new string('0',64),
            s => s["document"]!["activityCoverage"]![1]!["state"] = "paused",
            s => s["document"]!["activityCoverage"]![1]!["recordedAtUtc"] = Time(2).ToString("O"),
            s => s["document"]!["activityCoverage"]![1]!["previousTaskRevision"] = 0,
            s => s["document"]!["activityCoverage"]![2]!["previousTaskRevision"] = 2,
            s => s["document"]!["habitScores"]!.AsArray().RemoveAt(0),
            s => s["events"]!.AsArray().RemoveAt(1)
        };
        foreach (var change in corruptions) { var json = JsonNode.Parse(WorkspaceJournalJson.Serialize(state))!.AsObject(); change(json); Assert.ThrowsAny<Exception>(() => WorkspaceJournalJson.Deserialize(json.ToJsonString())); }
        Assert.Equal(3, state.Document.HabitScores.Count);
    }

    [Fact]
    public void Metadata_revisions_preserve_chain_but_unobserved_task_changes_leave_a_gap()
    {
        var state = Start(); state = state with { Document = state.Document with { Revision = state.Document.Revision + 1 }, StoreVersion = state.StoreVersion + 1 };
        state = Apply(state, WorkspaceOperation.UpdateTask, 1, new(Text: "metadata only")); Assert.All(Read(state, through: 1), row => Assert.True(row.IsKnown));
        var revision = state.Document.Revision + 1;
        state = state with { Document = state.Document with { Revision = revision, Tasks = [state.Document.Tasks[0] with { Revision = revision, UpdatedAtUtc = Time(2), State = CompanionTaskState.Paused }] }, StoreVersion = revision };
        state = Apply(state, WorkspaceOperation.ResumeTask, 3); state = Apply(state, WorkspaceOperation.UpdateTask, 4, new(Text: "observed suffix"));
        var intervals = Read(state, through: 4); Assert.True(intervals[0].IsKnown); Assert.False(intervals[1].IsKnown);
        Assert.Equal(Time(1), intervals[1].FromUtc); Assert.Equal(Time(3), intervals[1].ThroughUtc); Assert.True(intervals[2].IsKnown);
    }

    [Fact]
    public void Lost_terminal_occurrence_is_not_covered_inactivity()
    {
        var state = Start(); state = Apply(state, WorkspaceOperation.SkipOccurrence, 1); state = Apply(state, WorkspaceOperation.UpdateTask, 2, new(Text: "tail"));
        var bad = state with { Document = state.Document with { Occurrences = [] } }; Assert.False(WorkspaceJournalValidation.IsValid(bad));
        Assert.Throws<ArgumentException>(() => Read(bad));
    }

    [Fact]
    public void Current_state_and_text_only_edits_cannot_contradict_causal_schedule()
    {
        var state = Apply(Start(), WorkspaceOperation.UpdateTask, 1, new(Text: "tail"));
        var changed = state.Document.Tasks[0] with { Recurrence = Rule("Europe/Prague") };
        var bad = state with { Document = state.Document with { Tasks = [changed] } };
        Assert.False(WorkspaceJournalValidation.IsValid(bad));
        var rows = state.Document.ActivityCoverage!.ToArray(); rows[1] = rows[1] with { Recurrence = changed.Recurrence };
        bad = bad with { Document = bad.Document with { ActivityCoverage = rows }, Commands = state.Commands.Select((e, i) => i == 1 ? e with { TaskSnapshot = changed } : e).ToArray() };
        Assert.False(WorkspaceJournalValidation.IsValid(bad));
        Assert.False(WorkspaceJournalValidation.IsValid(state with { Document = state.Document with { Tasks = [state.Document.Tasks[0] with { State = CompanionTaskState.Paused }] } }));
    }

    [Fact]
    public void An_unrelated_score_cannot_invent_activity_at_a_covered_revision()
    {
        var state = Apply(Start(CompanionTaskKind.Habit), WorkspaceOperation.UpdateTask, 1, new(Text: "tail"));
        var bad = state with { Document = state.Document with { HabitScores = [new(Guid.NewGuid(), state.Commands[0].TaskSnapshot!.Revision, state.Document.Tasks[0].Id, Guid.NewGuid(), Time(0), HabitScoreDirection.Up)] } };
        Assert.False(WorkspaceJournalValidation.IsValid(bad)); Assert.Empty(state.Document.HabitScores);
    }

    [Fact]
    public void Clock_rollback_leaves_chronological_intervals_unknown()
    {
        var state = Start(); state = Apply(state, WorkspaceOperation.UpdateTask, 2, new(Text: "later")); state = Apply(state, WorkspaceOperation.UpdateTask, 1, new(Text: "rollback"));
        Assert.Equal("clock_order_unknown", Assert.Single(Read(state)).UnknownReason);
    }

    [Fact]
    public void Worker_neutral_fixture_round_trips_schedules_corrections_and_archive()
    {
        var state = WorkspaceJournalJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "activity-coverage-v1.json")));
        var result = Read(state, through: 8); Assert.Equal(8, result.Count); Assert.All(result, row => Assert.True(row.IsKnown));
        Assert.Equal(CompanionTaskState.Paused, result[2].Schedule!.State); Assert.True(result[6].Schedule!.IsArchived);
        Assert.Single(state.Document.OccurrenceCorrections!); Assert.Empty(state.Document.Completions); Assert.Empty(state.Document.HabitScores);
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
        var bad = state.Document.ActivityCoverage!.ToArray(); bad[0] = bad[0] with { Recurrence = Rule("Europe/Prague") };
        Assert.False(WorkspaceJournalValidation.IsValid(state with { Document = state.Document with { ActivityCoverage = bad } }));
    }
}
