using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoSyncPageTests : BunitContext
{
    private async Task<(WorkspaceTaskFixture Fixture, TodoWorkspacePresentation Todo)> SetupAsync()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        var fixture = new WorkspaceTaskFixture(); fixture.Client.Connected = true; fixture.Client.ExecutorAvailable = true;
        Services.AddSingleton<IAppSessionController>(fixture.Session);
        Services.AddSingleton<IWorkspaceCloudClient>(fixture.Client);
        Services.AddSingleton(fixture.Database);
        Services.AddSingleton(fixture.Controller);
        Services.AddSingleton(new WorkspaceDataController(fixture.Session, fixture.Client, fixture.Database));
        Services.AddSingleton<TodoTaskDraftStore>();
        var todo = new TodoWorkspacePresentation(fixture.Session, fixture.Client, fixture.Database, fixture.Controller);
        Services.AddSingleton(todo);
        Services.AddSingleton(new PushDeviceController(fixture.Session,new PushReminderFixture.Client(),new PushReminderFixture.Browser(),new PushReminderFixture.Store()));
        await fixture.Controller.LoadAsync(true);
        return (fixture, todo);
    }

    [Fact]
    public async Task Sync_page_uses_shared_source_and_policy_without_automatic_writes()
    {
        var (fixture, _) = await SetupAsync();
        var cut = Render<Habitica.WebApp.Pages.TodoSyncPage>();
        Assert.NotNull(cut.Find("[data-testid='todo-source']"));
        Assert.NotNull(cut.Find("[data-testid='todo-command-history']"));
        Assert.NotNull(cut.Find("[data-testid='workspace-tags']"));
        Assert.NotNull(cut.Find("[data-testid='workspace-executor-enable']"));
        Assert.Empty(fixture.Client.PreparationIds);
        Assert.Empty(fixture.Client.AcceptedIds);
        Habitica.WebApp.Tests.Qualification.PageLayoutEvidence.Save("todo-sync-overview", cut.Markup, "/todo/sync", "source-history-consent", ["navigation", "form", "disabled-controls"], nameof(Sync_page_uses_shared_source_and_policy_without_automatic_writes));
    }

    [Fact]
    public async Task Exact_original_receipt_read_keeps_lost_outcome_distinct_and_discards_old_account_response()
    {
        var (fixture, todo) = await SetupAsync();
        await fixture.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());
        await fixture.Controller.ConfirmAsync();
        await todo.LoadSelectedAsync();
        Assert.NotNull(todo.Snapshot);
        var command = Assert.Single(todo.Snapshot.Commands).Command;
        var acceptedBeforeLookup = fixture.Client.AcceptedIds.Count;
        var cut = Render<TodoCommandHistoryPanel>();
        Assert.Contains("1 retained commands", cut.Markup);
        cut.Find("[data-testid='todo-command-id']").Input(command.Id.ToString("D"));
        cut.Find("[data-testid='todo-command-lookup']").Click();
        Assert.Contains("saved in Companion", cut.Find("[data-testid='todo-command-receipt']").TextContent);
        Habitica.WebApp.Tests.Qualification.PageLayoutEvidence.Save("todo-sync-original-receipt", cut.Markup, "/todo/sync", "original-receipt-no-retry", ["navigation", "form", "status"], nameof(Exact_original_receipt_read_keeps_lost_outcome_distinct_and_discards_old_account_response));
        Assert.Equal([command.Id], fixture.Client.ReceiptReads);
        Assert.Equal(acceptedBeforeLookup, fixture.Client.AcceptedIds.Count);

        var pending = new TaskCompletionSource<WorkspaceTaskResponse<WorkspaceMutationReceipt>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Client.ReceiptReadAction = _ => pending.Task;
        var lookup = cut.InvokeAsync(() => cut.Find("[data-testid='todo-command-lookup']").Click());
        await Task.Yield();
        await cut.InvokeAsync(() => fixture.Session.SetState(fixture.Session.State with { UserId = Guid.NewGuid().ToString("D") }));
        pending.SetResult(new(true, WorkspaceTaskController.Receipt(fixture.Client.State.Commands.Single(), Guid.Empty) with { TaskText = "PRIVATE OLD ACCOUNT" }, "Old receipt"));
        await lookup.WaitAsync(TimeSpan.FromSeconds(3));
        cut.WaitForAssertion(() => Assert.DoesNotContain("PRIVATE OLD ACCOUNT", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='todo-command-receipt']"));
    }

    [Fact]
    public async Task Missing_browser_receipt_never_creates_or_retries_a_command()
    {
        var fixture = new WorkspaceTaskFixture();
        await fixture.SeedLegacyLocalAsync();
        var controller = new WorkspaceDataController(fixture.Session, fixture.Client, fixture.Database);
        var id = fixture.Controller.Snapshot!.Document.Id;
        var before = fixture.Js.Rows.Count;
        var missing = await controller.ReadTaskReceiptAsync(false, id, Guid.NewGuid());
        Assert.False(missing.Succeeded); Assert.True(missing.OutcomeUnknown); Assert.Null(missing.Value);
        Assert.Equal(before, fixture.Js.Rows.Count);
        Assert.Empty(fixture.Client.AcceptedIds);
    }
}
