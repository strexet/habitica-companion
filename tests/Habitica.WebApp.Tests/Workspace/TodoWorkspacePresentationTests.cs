using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.State;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoWorkspacePresentationTests
{
    private static TodoWorkspacePresentation Presentation(WorkspaceTaskFixture f) => new(f.Session, f.Client, f.Database, f.Controller);

    [Fact]
    public async Task Coverage_updates_after_a_save_and_does_not_leak_between_workspaces()
    {
        var f = new WorkspaceTaskFixture(); using var presentation = Presentation(f);
        await f.Controller.CreateLocalAsync();
        var first = f.Controller.Snapshot!.Document.Id;
        await presentation.RefreshSourcesAsync();
        await presentation.SelectAsync(new(TodoWorkspaceSource.Browser, first));
        var empty = presentation.HabiticaCoverage;
        Assert.True(empty!.HasCompleteStoredCoverage);
        Assert.Same(empty, presentation.HabiticaCoverage);
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());
        await f.Controller.ConfirmAsync();
        Assert.Equal(1, presentation.HabiticaCoverage!.PendingTaskLinks);
        await f.Controller.CreateLocalAsync();
        await presentation.RefreshSourcesAsync();
        await presentation.SelectAsync(new(TodoWorkspaceSource.Browser, f.Controller.Snapshot!.Document.Id));
        Assert.Empty(presentation.HabiticaCoverage!.Tasks);
        await presentation.SelectAsync(new(TodoWorkspaceSource.Browser, first));
        Assert.Equal(1, presentation.HabiticaCoverage!.PendingTaskLinks);
        Assert.Empty(f.Client.AcceptedIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_source_load_cannot_replace_a_new_selection_or_a_new_original_preview(bool prepareDuringRead)
    {
        var f = new WorkspaceTaskFixture(); await f.Controller.CreateLocalAsync();
        var local = f.Controller.Snapshot!.Document.Id; f.Client.Connected = true;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Client.LoadAction = async _ => { entered.TrySetResult(); await release.Task; return f.Client.State; };
        var pending = f.Controller.LoadAsync(true); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Guid? original = null;
        if (prepareDuringRead) { await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo()); original = f.Controller.Preview!.PreparedCommandId; }
        else await f.Controller.LoadAsync(false, local);
        release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(f.Controller.IsCloud); Assert.Equal(local, f.Controller.Snapshot!.Document.Id);
        Assert.Equal(original, f.Controller.Preview?.PreparedCommandId); Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Linked_tasks_keep_distinct_canonical_and_Habitica_ids_without_duplicating_cached_rows()
    {
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        var id = Guid.NewGuid(); var external = Guid.NewGuid();
        var accepted = WorkspaceCommandEngine.Accept(f.Client.State,
            new(Guid.NewGuid(), f.Client.State.Document.Id, 1, WorkspaceOperation.CreateTodo, id, new(Text: "Mapped Todo")), DateTimeOffset.UtcNow).Snapshot!;
        var document = accepted.Document;
        f.Client.State = accepted with { Document = document with
        {
            Projections = [new(Guid.NewGuid(), document.Revision, id, ProjectionState.Linked, external.ToString("D"))],
            Goals = [new(Guid.NewGuid(), document.Revision, "Mapped goal", [id])]
        } };
        f.Session.SetState(f.Session.State with { TaskSnapshot = new(DateTimeOffset.UtcNow,
            [new(external.ToString("D"), "Mapped Todo", Habitica.Domain.Tasks.TaskType.Todo, false, 1, null, null)]) });
        using var presentation = Presentation(f); await presentation.RefreshSourcesAsync();
        await presentation.SelectAsync(new(TodoWorkspaceSource.Cloud, document.Id));
        Assert.Equal(id, Assert.Single(presentation.Tasks(new()).Tasks).Id);
        Assert.Equal(id, Assert.Single(presentation.Hierarchy(new()).Items).Task.Id);
        Assert.Throws<InvalidOperationException>(() => presentation.Hierarchy(new(TaskId: external)));
        Assert.Equal(external.ToString("D"), presentation.Provenance(id).HabiticaTaskId);
        Assert.Throws<InvalidOperationException>(() => presentation.Provenance(external));
        Assert.Equal(1, Assert.Single(presentation.Goals(new()).Items).EligibleTodos);
        Assert.NotNull(presentation.HabiticaCache.RetrievedAtUtc);
        Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Cancelled_load_cannot_expose_an_ignored_late_response_as_current_data()
    {
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        using var presentation = Presentation(f); await presentation.RefreshSourcesAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Client.LoadAction = async _ => { entered.TrySetResult(); await release.Task; return f.Client.State; };
        using var cancellation = new CancellationTokenSource();
        var pending = presentation.SelectAsync(presentation.Sources.Single(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel(); release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Null(presentation.Snapshot); Assert.False(presentation.IsBusy);
        Assert.Equal(TodoWorkspaceAvailability.Unavailable, presentation.Availability);
        f.Client.LoadAction = null; await presentation.LoadSelectedAsync();
        Assert.Equal(TodoWorkspaceAvailability.Available, presentation.Availability); Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Invalid_snapshot_is_unavailable_rather_than_an_empty_canonical_result()
    {
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        f.Client.State = f.Client.State with { Document = f.Client.State.Document with { Revision = 0 } };
        using var presentation = Presentation(f); await presentation.RefreshSourcesAsync();
        await presentation.SelectAsync(presentation.Sources.Single());
        Assert.Equal(TodoWorkspaceAvailability.Unavailable, presentation.Availability);
        Assert.Null(presentation.Revision); Assert.Throws<InvalidOperationException>(() => presentation.Tasks(new()));
        Assert.Throws<InvalidOperationException>(() => presentation.Hierarchy(new()));
        Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Unavailable_browser_listing_remains_distinct_and_does_not_block_existing_cloud_access()
    {
        var f = new WorkspaceTaskFixture(); f.Js.FailLists = true;
        using var presentation = Presentation(f); await presentation.RefreshSourcesAsync();
        Assert.False(presentation.BrowserSourcesAvailable); Assert.Empty(presentation.Sources);
        Assert.Equal(TodoWorkspaceAvailability.Unavailable, presentation.Availability);
        f.Client.Connected = true; await presentation.RefreshSourcesAsync();
        Assert.Single(presentation.Sources); Assert.False(presentation.BrowserSourcesAvailable);
        await presentation.SelectAsync(presentation.Sources[0]);
        Assert.Equal(TodoWorkspaceAvailability.Available, presentation.Availability);
        Assert.Equal(TodoWorkspaceSource.Cloud, presentation.Selection!.Source);
        Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Independent_browser_workspaces_require_exact_choice_and_preserve_reads_without_merging()
    {
        var f = new WorkspaceTaskFixture();
        await f.Controller.CreateLocalAsync(); var first = f.Controller.Snapshot!.Document.Id;
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo("First workspace")); await f.Controller.ConfirmAsync();
        await f.Controller.CreateLocalAsync(); var second = f.Controller.Snapshot!.Document.Id;
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo("Second workspace")); await f.Controller.ConfirmAsync();
        f.Controller.Forget();
        await f.Controller.LoadAsync(false);
        Assert.Null(f.Controller.Snapshot); Assert.Contains("Choose the exact", f.Controller.Message);
        using var presentation = Presentation(f);
        await presentation.RefreshSourcesAsync();
        Assert.Equal(2, presentation.Sources.Count); Assert.Equal(TodoWorkspaceAvailability.NeedsSelection, presentation.Availability);
        await Assert.ThrowsAsync<InvalidOperationException>(() => presentation.LoadSelectedAsync());
        await presentation.SelectAsync(new(TodoWorkspaceSource.Browser, first));
        Assert.Equal("First workspace", Assert.Single(presentation.Tasks(new()).Tasks).Text);
        var revision = presentation.Revision;
        await presentation.RefreshSourcesAsync(); Assert.Equal(first, presentation.Selection!.WorkspaceId);
        Assert.Equal(revision, presentation.Tasks(new(ExpectedRevision: revision)).Revision);
        Assert.Throws<InvalidOperationException>(() => presentation.Tasks(new(ExpectedRevision: revision + 1)));
        await presentation.SelectAsync(new(TodoWorkspaceSource.Browser, second));
        Assert.Equal("Second workspace", Assert.Single(presentation.Tasks(new()).Tasks).Text);
        Assert.Empty(f.Client.PreparationIds); Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Empty_available_data_differs_from_failed_local_load_and_source_listing_cannot_hide_failure()
    {
        var f = new WorkspaceTaskFixture(); await f.Controller.CreateLocalAsync();
        using var presentation = Presentation(f);
        Assert.Equal(TodoWorkspaceAvailability.Available, presentation.Availability);
        Assert.Empty(presentation.Tasks(new()).Tasks); Assert.Empty(presentation.History(new()).Items);
        Assert.Empty(presentation.Projects(new()).Items); Assert.Empty(presentation.Goals(new()).Items);
        f.Js.FailReads = true;
        await Assert.ThrowsAnyAsync<Exception>(() => presentation.LoadSelectedAsync());
        Assert.Equal(TodoWorkspaceAvailability.Unavailable, presentation.Availability);
        Assert.Throws<InvalidOperationException>(() => presentation.Tasks(new()));
        Assert.Throws<InvalidOperationException>(() => presentation.Hierarchy(new()));
        await presentation.RefreshSourcesAsync();
        Assert.Equal(TodoWorkspaceAvailability.Unavailable, presentation.Availability);
        f.Js.FailReads = false; await presentation.LoadSelectedAsync();
        Assert.Equal(TodoWorkspaceAvailability.Available, presentation.Availability);
        Assert.Empty(presentation.Agenda(new(new(2026, 10, 20), new(2026, 10, 20))).Items);
    }

    [Theory]
    [InlineData(WorkspaceAuthorityMode.Frozen)]
    [InlineData(WorkspaceAuthorityMode.Cloud)]
    public async Task Frozen_and_cloud_owned_browser_copies_remain_readable_without_local_writes(WorkspaceAuthorityMode mode)
    {
        var f = new WorkspaceTaskFixture(); await f.Controller.CreateLocalAsync();
        var original = f.Controller.Snapshot!;
        var frozen = original with { StoreVersion = original.StoreVersion + 1, Authority = new(mode, Guid.NewGuid(), mode == WorkspaceAuthorityMode.Cloud ? original.Document.Id : null) };
        Assert.True(await f.Database.ForScope(WorkspaceTaskFixture.User).TryCommitAsync(original.StoreVersion, frozen, default));
        using var presentation = Presentation(f); await presentation.LoadSelectedAsync();
        Assert.True(presentation.IsReadOnly); Assert.NotNull(presentation.Snapshot);
        await Assert.ThrowsAsync<InvalidOperationException>(() => presentation.PrepareAsync(WorkspaceTaskFixture.Todo()));
        Assert.Null(f.Controller.Preview); Assert.Empty(f.Client.PreparationIds); Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Canonical_mapping_and_original_unknown_command_survive_navigation_and_unavailable_cloud_reload()
    {
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        var presentation = Presentation(f);
        await presentation.RefreshSourcesAsync(); await presentation.SelectAsync(new(TodoWorkspaceSource.Cloud, f.Client.State.Document.Id));
        await presentation.PrepareAsync(WorkspaceTaskFixture.Todo() with { ProjectToHabitica = true });
        var original = presentation.Commands.Preview!.PreparedCommandId;
        f.Client.LoseAcceptance = true; await presentation.ConfirmAsync();
        Assert.True(presentation.Commands.AcceptanceUnknown); Assert.False(presentation.CanSwitchSource);
        presentation.Dispose();
        using var nextPage = Presentation(f);
        Assert.Equal(original, nextPage.Commands.Preview!.PreparedCommandId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => nextPage.CreateBrowserAsync());
        f.Client.FailLoads = true; await nextPage.LoadSelectedAsync();
        Assert.Equal(TodoWorkspaceAvailability.Unavailable, nextPage.Availability);
        Assert.Equal(original, nextPage.Commands.Preview!.PreparedCommandId);
        f.Client.FailLoads = false; await nextPage.CheckOriginalAsync();
        Assert.Equal(original, nextPage.Commands.LastReceipt!.CommandId);
        Assert.Equal(TodoWorkspaceAvailability.Available, nextPage.Availability);
        var task = Assert.Single(nextPage.Tasks(new()).Tasks); var mapping = nextPage.Provenance(task.Id);
        Assert.Equal(task.Id, mapping.TaskId); Assert.Equal(TodoWorkspaceSource.Cloud, mapping.Source);
        Assert.Equal(ProjectionState.Pending, mapping.MappingState); Assert.Equal(nextPage.Revision, mapping.WorkspaceRevision);
        Assert.Single(f.Client.AcceptedIds); Assert.Single(f.Client.PreparationIds);
        Assert.Single(nextPage.Snapshot!.Document.Tasks);
        Assert.Throws<InvalidOperationException>(() => nextPage.Provenance(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_read_cannot_replace_new_account_data_even_after_same_account_sign_in(bool sameAccount)
    {
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        using var presentation = Presentation(f); await presentation.RefreshSourcesAsync();
        var old = f.Client.State; var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Client.LoadAction = async _ => { entered.TrySetResult(); await release.Task; return old; };
        var pending = presentation.SelectAsync(new(TodoWorkspaceSource.Cloud, old.Document.Id)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Session.SetState(SessionViewModel.Empty);
        Assert.Equal(TodoWorkspaceAvailability.SignedOut, presentation.Availability); Assert.Null(presentation.Snapshot);
        Assert.Null(presentation.Commands.Preview); Assert.Empty(presentation.Sources);
        f.Session.SetState(SessionViewModel.Empty with { IsAuthenticated = true, UserId = sameAccount ? WorkspaceTaskFixture.User : "00000000-0000-4000-8000-000000000012" });
        var fresh = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, [], []);
        f.Client.State = fresh; f.Client.LoadAction = _ => Task.FromResult<WorkspaceJournalSnapshot?>(fresh);
        await presentation.RefreshSourcesAsync(); await presentation.SelectAsync(new(TodoWorkspaceSource.Cloud, fresh.Document.Id));
        release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(fresh.Document.Id, presentation.Snapshot!.Document.Id);
        Assert.Equal(TodoWorkspaceAvailability.Available, presentation.Availability);
        Assert.False(presentation.IsBusy); Assert.Empty(f.Client.AcceptedIds);
    }
}
