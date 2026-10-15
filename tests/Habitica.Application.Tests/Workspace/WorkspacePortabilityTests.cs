using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspacePortabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
    private static WorkspaceJournalSnapshot Accepted()
    {
        var empty = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, Array.Empty<WorkspaceCommandEntry>());
        var command = new WorkspaceCommand(Guid.NewGuid(), empty.Document.Id, 1, WorkspaceOperation.CreateTodo, Guid.NewGuid(), new(Text: "Portable task", Notes: "Private notes"), ProjectToHabitica: true);
        return WorkspaceCommandEngine.Accept(empty, command, Now).Snapshot!;
    }

    [Fact]
    public async Task ExportAndRestorePreserveCanonicalIdentitiesCommandsAndHistory()
    {
        var original = Accepted(); var source = new Store(original);
        var json = await new WorkspacePortabilityService(source).ExportAsync(original.Document.Id, default);
        var target = new Store(); var service = new WorkspacePortabilityService(target);
        Assert.Equal(WorkspaceRestoreOutcome.Restored, await service.RestoreAsync(json!, default));
        var restored = target.Read(); Assert.Equal(original.Document.Id, restored!.Document.Id);
        Assert.Equal(original.Document.Tasks[0].Id, restored.Document.Tasks[0].Id);
        Assert.Equal(original.Commands[0].Command.Id, restored.Commands[0].Command.Id);
        Assert.Equal(original.Events![0].Id, restored.Events![0].Id);
        Assert.Equal(WorkspaceRestoreOutcome.Unchanged, await service.RestoreAsync(json!, default));
        Assert.Equal(WorkspaceAuthorityMode.Paused, restored.Authority!.Mode);
        Assert.False(ProjectionJournal.Acquire(restored, restored.Commands[0].Command.Id, "copied-browser", Now, TimeSpan.FromSeconds(30)).Acquired);
        Assert.DoesNotContain("apiToken", json!); Assert.DoesNotContain("managementToken", json!);
    }

    [Fact]
    public async Task ExistingDifferentWorkspaceStateIsNotSilentlyReplaced()
    {
        var original = Accepted(); var command = original.Commands[0].Command;
        var changed = WorkspaceCommandEngine.Accept(original, new(Guid.NewGuid(), original.Document.Id, original.Document.Revision, WorkspaceOperation.UpdateTask, command.TaskId, new(Text: "Newer local edit")), Now).Snapshot!;
        var target = new Store(changed); var before = target.Json;
        Assert.Equal(WorkspaceRestoreOutcome.Conflict, await new WorkspacePortabilityService(target).RestoreAsync(WorkspaceJournalJson.Serialize(original), default));
        Assert.Equal(before, target.Json);
    }

    [Fact]
    public async Task RestoredInflightEffectsBecomeUnknownAndUndispatchedLeasesBecomePending()
    {
        var original = Accepted(); var id = original.Commands[0].Command.Id;
        var lease = ProjectionJournal.Acquire(original, id, "fixture", Now, TimeSpan.FromSeconds(30));
        var target = new Store(); var clock = new Clock(Now.AddMinutes(1));
        await new WorkspacePortabilityService(target, clock).RestoreAsync(WorkspaceJournalJson.Serialize(lease.Snapshot), default);
        Assert.Equal(ProjectionCommandState.Pending, target.Read()!.Commands[0].ProjectionState);
        var executing = ProjectionJournal.Dispatch(lease.Snapshot, id, lease.Entry!.Lease!.Token, Now);
        target = new Store();
        await new WorkspacePortabilityService(target, clock).RestoreAsync(WorkspaceJournalJson.Serialize(executing), default);
        var unknown = target.Read()!; Assert.Equal(ProjectionCommandState.UnknownOutcome, unknown.Commands[0].ProjectionState);
        Assert.False(ProjectionJournal.Acquire(unknown, id, "new-owner", Now.AddHours(1), TimeSpan.FromSeconds(30)).Acquired);
        Assert.Contains(unknown.Events!, value => value.Kind == JournalEventKind.UnknownDetected);
        Assert.Empty(WorkspaceRules.Validate(unknown.Document));
    }

    [Fact]
    public async Task InvalidImportNeverWritesOrReadsAReplacementSnapshot()
    {
        var target = new Store(); var service = new WorkspacePortabilityService(target);
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.RestoreAsync("{\"document\":null,\"storeVersion\":1,\"commands\":[]}", default));
        Assert.Null(target.Json);
    }

    private sealed class Clock(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
    private sealed class Store : ICompanionWorkspaceStore
    {
        public string? Json { get; private set; }
        public Store(WorkspaceJournalSnapshot? value = null) { if (value is not null) Json = WorkspaceJournalJson.Serialize(value); }
        public WorkspaceJournalSnapshot? Read() => Json is null ? null : WorkspaceJournalJson.Deserialize(Json);
        public Task<WorkspaceJournalSnapshot?> LoadAsync(Guid id, CancellationToken token) { var value = Read(); return Task.FromResult(value?.Document.Id == id ? value : null); }
        public Task<bool> TryCommitAsync(long expected, WorkspaceJournalSnapshot value, CancellationToken token)
        {
            if ((Read()?.StoreVersion ?? 0) != expected) return Task.FromResult(false);
            Json = WorkspaceJournalJson.Serialize(value); return Task.FromResult(true);
        }
    }
}
