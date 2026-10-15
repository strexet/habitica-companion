using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceHandoffTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
    private static WorkspaceJournalSnapshot Accepted()
    {
        var empty = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, []);
        return WorkspaceCommandEngine.Accept(empty, new(Guid.NewGuid(), empty.Document.Id, 1, WorkspaceOperation.CreateTodo, Guid.NewGuid(), new(Text: "Transfer intent"), ProjectToHabitica: true), Now).Snapshot!;
    }
    [Fact]
    public async Task FrozenTransferSurvivesRestartAndLostAcknowledgementWithoutLocalDispatch()
    {
        var store = new Store(Accepted()); var id = store.Value.Document.Id;
        var transfer = new WorkspaceHandoffService(store, new Clock(Now));
        var json = await transfer.PrepareAsync(id, default); var frozen = WorkspaceJournalJson.Deserialize(json);
        Assert.Equal(WorkspaceAuthorityMode.Frozen, frozen.Authority!.Mode);
        Assert.False(ProjectionJournal.Acquire(store.Value, frozen.Commands[0].Command.Id, "local", Now, TimeSpan.FromSeconds(30)).Acquired);
        Assert.Equal(json, await new WorkspaceHandoffService(store, new Clock(Now.AddMinutes(1))).PrepareAsync(id, default));
        var edit = new WorkspaceCommand(Guid.NewGuid(), id, frozen.Document.Revision, WorkspaceOperation.UpdateTask, frozen.Commands[0].Command.TaskId, new(Text: "Cannot alter frozen archive"));
        Assert.Equal("workspace_owned_elsewhere", WorkspaceCommandEngine.Accept(store.Value, edit, Now).ErrorCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transfer.AcknowledgeAsync(id, Guid.NewGuid(), default));
        await transfer.AcknowledgeAsync(id, frozen.Authority.TransferId!.Value, default);
        Assert.Equal(WorkspaceAuthorityMode.Cloud, store.Value.Authority!.Mode);
        await transfer.AcknowledgeAsync(id, frozen.Authority.TransferId.Value, default);
        Assert.False(ProjectionJournal.Acquire(store.Value, frozen.Commands[0].Command.Id, "local", Now.AddDays(1), TimeSpan.FromSeconds(30)).Acquired);
    }
    [Fact]
    public async Task InflightLocalRequestMustFinishOrExpireBeforeTransferAndCopiedEffectStaysUnknown()
    {
        var initial = Accepted(); var commandId = initial.Commands[0].Command.Id;
        var acquired = ProjectionJournal.Acquire(initial, commandId, "local", Now, TimeSpan.FromSeconds(30));
        var executing = ProjectionJournal.Dispatch(acquired.Snapshot, commandId, acquired.Entry!.Lease!.Token, Now);
        var store = new Store(executing); var service = new WorkspaceHandoffService(store, new Clock(Now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(initial.Document.Id, default));
        Assert.Equal(WorkspaceAuthorityMode.Frozen, store.Value.Authority!.Mode);
        Assert.Throws<InvalidOperationException>(() => ProjectionJournal.Dispatch(store.Value, commandId, acquired.Entry.Lease.Token, Now));
        var json = await new WorkspaceHandoffService(store, new Clock(Now.AddMinutes(1))).PrepareAsync(initial.Document.Id, default);
        var recovered = WorkspaceJournalJson.Deserialize(json);
        Assert.Equal(ProjectionCommandState.UnknownOutcome, recovered.Commands[0].ProjectionState);
        Assert.Equal(acquired.Entry.Lease.Token, recovered.Commands[0].Lease!.Token);
        Assert.Equal(1, recovered.Commands[0].Attempts);
    }
    [Fact]
    public async Task FreezeFailureNeverReturnsAnUploadableArchive()
    {
        var store = new Store(Accepted()) { FailCommit = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new WorkspaceHandoffService(store).PrepareAsync(store.Value.Document.Id, default));
        Assert.Null(store.Value.Authority);
    }
    private sealed class Clock(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
    private sealed class Store(WorkspaceJournalSnapshot value) : ICompanionWorkspaceStore
    {
        public WorkspaceJournalSnapshot Value { get; private set; } = value;
        public bool FailCommit { get; init; }
        public Task<WorkspaceJournalSnapshot?> LoadAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<WorkspaceJournalSnapshot?>(Value.Document.Id == id ? Value : null);
        public Task<bool> TryCommitAsync(long expected, WorkspaceJournalSnapshot snapshot, CancellationToken cancellationToken)
        {
            if (FailCommit || expected != Value.StoreVersion) return Task.FromResult(false);
            Assert.True(WorkspaceJournalValidation.IsValid(snapshot)); Value = snapshot; return Task.FromResult(true);
        }
    }
}
