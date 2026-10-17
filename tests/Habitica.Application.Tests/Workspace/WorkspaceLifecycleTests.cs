using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
    private static WorkspaceJournalSnapshot Empty() => new(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, []);
    private static WorkspaceCommand Command(WorkspaceJournalSnapshot state, WorkspaceOperation operation, Guid taskId, WorkspaceTaskPatch? patch = null) => new(Guid.NewGuid(), state.Document.Id, state.Document.Revision, operation, taskId, patch);

    [Theory]
    [InlineData(WorkspaceOperation.CreateDaily, CompanionTaskKind.Daily)]
    [InlineData(WorkspaceOperation.CreateHabit, CompanionTaskKind.Habit)]
    public void NativeLifecycleKeepsHistoryAndIdempotentReceipts(WorkspaceOperation operation, CompanionTaskKind kind)
    {
        var state = Empty(); var id = Guid.NewGuid();
        var create = Command(state, operation, id, new(Text: "Disposable", Deadline: new(2026, 10, 18)));
        var accepted = WorkspaceCommandEngine.Accept(state, create, Now);
        Assert.Equal(SubmissionDisposition.Accepted, accepted.Disposition); state = accepted.Snapshot!;
        var task = Assert.Single(state.Document.Tasks); Assert.Equal(kind, task.Kind); Assert.Null(task.Recurrence);
        Assert.Empty(accepted.Entry!.Effects); Assert.Equal(ProjectionCommandState.NotRequested, accepted.Entry.ProjectionState);
        var update = Command(state, WorkspaceOperation.UpdateTask, id, new(Notes: "Changed description", ClearDeadline: true));
        state = WorkspaceCommandEngine.Accept(state, update, Now).Snapshot!;
        var eventCommand = kind == CompanionTaskKind.Daily
            ? Command(state, WorkspaceOperation.CompleteTask, id) with { OccurrenceDate = new(2026, 10, 17) }
            : Command(state, WorkspaceOperation.ScoreHabit, id) with { Direction = HabitScoreDirection.Down, Count = 2 };
        state = WorkspaceCommandEngine.Accept(state, eventCommand, Now).Snapshot!;
        var delete = Command(state, WorkspaceOperation.DeleteTask, id);
        var deleted = WorkspaceCommandEngine.Accept(state, delete, Now); state = deleted.Snapshot!;
        task = Assert.Single(state.Document.Tasks); Assert.True(task.IsArchived); Assert.Null(task.Deadline); Assert.Equal("Changed description", task.Notes);
        Assert.Equal(4, state.Commands.Count); Assert.Equal(kind == CompanionTaskKind.Daily ? 1 : 0, state.Document.Completions.Count); Assert.Equal(kind == CompanionTaskKind.Habit ? 2 : 0, state.Document.HabitScores.Count);
        Assert.Empty(WorkspaceRules.Validate(state.Document)); Assert.True(WorkspaceJournalValidation.IsValid(state));
        Assert.Equal(SubmissionDisposition.Duplicate, WorkspaceCommandEngine.Accept(state, delete, Now).Disposition);
        Assert.Equal(SubmissionDisposition.Duplicate, WorkspaceCommandEngine.Accept(state, create, Now).Disposition);
        Assert.Equal("task_unavailable", WorkspaceCommandEngine.Accept(state, Command(state, WorkspaceOperation.UpdateTask, id, new(Text: "Revive")), Now).ErrorCode);
    }

    [Fact]
    public void UnsupportedProjectionAndStaleDeletionCannotChangeCanonicalState()
    {
        var state = Empty(); var id = Guid.NewGuid();
        foreach (var operation in new[] { WorkspaceOperation.CreateDaily, WorkspaceOperation.CreateHabit })
            Assert.Equal("unsupported_projection", WorkspaceCommandEngine.Accept(state, Command(state, operation, id, new(Text: "Unsupported")) with { ProjectToHabitica = true }, Now).ErrorCode);
        state = WorkspaceCommandEngine.Accept(state, Command(state, WorkspaceOperation.CreateTodo, id, new(Text: "Linked")) with { ProjectToHabitica = true }, Now).Snapshot!;
        Assert.Equal("unsupported_projection", WorkspaceCommandEngine.Accept(state, Command(state, WorkspaceOperation.DeleteTask, id), Now).ErrorCode);
        var unlinked = Empty(); var habit = Guid.NewGuid();
        unlinked = WorkspaceCommandEngine.Accept(unlinked, Command(unlinked, WorkspaceOperation.CreateHabit, habit, new(Text: "Native")), Now).Snapshot!;
        var delete = Command(unlinked, WorkspaceOperation.DeleteTask, habit);
        Assert.Equal("unsupported_projection", WorkspaceCommandEngine.Accept(unlinked, delete with { ProjectToHabitica = true }, Now).ErrorCode);
        unlinked = WorkspaceCommandEngine.Accept(unlinked, Command(unlinked, WorkspaceOperation.UpdateTask, habit, new(Text: "Changed")), Now).Snapshot!;
        Assert.Equal(SubmissionDisposition.RevisionConflict, WorkspaceCommandEngine.Accept(unlinked, delete, Now).Disposition);
        Assert.False(Assert.Single(unlinked.Document.Tasks).IsArchived);
    }
    [Fact]
    public void NewOperationHashesRoundTripThroughNeutralWorkerFixtures()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/lifecycle-commands.json")));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        foreach (var row in json.RootElement.EnumerateArray())
        {
            var command = row.GetProperty("command").Deserialize<WorkspaceCommand>(options)!;
            Assert.True(Enum.IsDefined(command.Operation));
            Assert.Equal(row.GetProperty("payloadHash").GetString(), WorkspaceCommandEngine.Hash(command));
        }
    }

}
