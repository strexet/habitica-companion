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

    [Theory]
    [InlineData("workspace-readiness-today", "/todo/today", CompanionTaskKind.Todo, false)]
    [InlineData("workspace-readiness-habits", "/todo/habits", CompanionTaskKind.Habit, true)]
    [InlineData("workspace-readiness-dailies", "/todo/dailies", CompanionTaskKind.Daily, true)]
    public async Task Shared_readiness_region_retains_category_and_capture_context(string name,string route,CompanionTaskKind kind,bool capture)
    {
        var (fixture, _) = await SetupAsync();
        var cut=Render<TodoTaskPanel>(p=>p.Add(x=>x.Kind,kind).Add(x=>x.ShowCapture,capture));
        Assert.Contains("Cloud workspace",cut.Find("[data-testid='workspace-readiness']").TextContent);
        Assert.Empty(fixture.Client.AcceptedIds);
        Habitica.WebApp.Tests.Qualification.PageLayoutEvidence.Save(name,cut.Markup,route,"loaded-shared-panel",["status","form","disabled-controls"],nameof(Shared_readiness_region_retains_category_and_capture_context));
    }

    [Fact]
    public async Task Access_failure_exposes_one_recovery_action_before_source_details_and_reload_recovers()
    {
        var (fixture, todo) = await SetupAsync();
        fixture.Client.FailLoads = true; await todo.LoadSelectedAsync();
        var cut = Render<TodoTaskPanel>();
        var status = cut.Find("[data-testid='workspace-readiness']");
        Assert.Contains("Cloud workspace", status.TextContent); Assert.Contains("unavailable", status.TextContent);
        Assert.True(cut.Markup.IndexOf("workspace-readiness", StringComparison.Ordinal) < cut.Markup.IndexOf("todo-source-picker", StringComparison.Ordinal));
        Assert.Single(status.QuerySelectorAll("button"));
        Habitica.WebApp.Tests.Qualification.PageLayoutEvidence.Save("workspace-readiness-unavailable", cut.Markup, "/todo/tasks", "terminal-load-failure", ["status", "recovery"], nameof(Access_failure_exposes_one_recovery_action_before_source_details_and_reload_recovers));
        fixture.Client.FailLoads = false; cut.Find("[data-testid='workspace-readiness-action']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Task editing is available", cut.Find("[data-testid='workspace-readiness']").TextContent));
        Assert.False(cut.Find("fieldset").HasAttribute("disabled"));
        Assert.Empty(fixture.Client.PreparationIds); Assert.Empty(fixture.Client.AcceptedIds);
        Habitica.WebApp.Tests.Qualification.PageLayoutEvidence.Save("workspace-readiness-ready", cut.Markup, "/todo/tasks", "loaded-source", ["status", "form"], nameof(Access_failure_exposes_one_recovery_action_before_source_details_and_reload_recovers));
    }

    [Fact]
    public async Task Readiness_review_action_focuses_original_heading_and_cancel_preserves_unaccepted_intent()
    {
        var (fixture, todo) = await SetupAsync();
        var cut = Render<TodoTaskPanel>();
        await cut.InvokeAsync(() => todo.PrepareAsync(WorkspaceTaskFixture.Todo("Retained review title")));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='todo-review']")));
        var original = todo.Commands.Preview!.PreparedCommandId;
        var heading = cut.Find("h3[data-testid='original-request-title']");
        Assert.Equal("Original request", heading.TextContent); Assert.Equal("-1", heading.GetAttribute("tabindex"));
        var before = JSInterop.Invocations.Count(row => row.Identifier == "Blazor._internal.domWrapper.focus");
        cut.Find("[data-testid='workspace-readiness-action']").Click();
        cut.WaitForAssertion(() => Assert.True(JSInterop.Invocations.Count(row => row.Identifier == "Blazor._internal.domWrapper.focus") > before));
        Assert.Equal(original, todo.Commands.Preview!.PreparedCommandId);
        cut.Find("[data-testid='todo-cancel']").Click();
        cut.WaitForAssertion(() => Assert.Equal("ready", todo.Readiness.Code));
        Assert.Empty(cut.FindAll("[data-testid='original-request-title']"));
        Assert.Empty(fixture.Client.AcceptedIds); Assert.Empty(fixture.Client.State.Commands);
    }

    [Fact]
    public async Task Reload_read_failure_does_not_invent_an_uncertain_save_or_original_receipt()
    {
        var (fixture, _) = await SetupAsync(); var cut = Render<TodoTaskPanel>();
        fixture.Client.LoadAction = _ => Task.FromException<WorkspaceJournalSnapshot?>(new IOException("Synthetic read failure"));
        cut.FindAll(".todo-source-controls button").Single(button => button.TextContent.Contains("Reload Workspace")).Click();
        cut.WaitForAssertion(() => Assert.Contains("Workspace data could not be loaded", cut.Markup));
        Assert.DoesNotContain("check its receipt before another save", cut.Markup);
        Assert.NotNull(cut.Find("[data-readiness='reload-required']"));
        Assert.Empty(fixture.Client.PreparationIds); Assert.Empty(fixture.Client.AcceptedIds);
    }

    [Fact]
    public async Task Loading_state_offers_wait_guidance_and_sync_does_not_link_to_itself()
    {
        var (fixture, todo) = await SetupAsync();
        var cut = Render<TodoTaskPanel>(p => p.Add(x=>x.ShowCapture, false).Add(x=>x.ShowSyncLink, false));
        fixture.Client.LoadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Client.LoadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = cut.InvokeAsync(() => todo.LoadSelectedAsync());
        await fixture.Client.LoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Contains("Wait", cut.Find("[data-testid='workspace-readiness']").TextContent));
        Assert.Empty(cut.Find("[data-testid='workspace-readiness']").QuerySelectorAll("button,a"));
        Habitica.WebApp.Tests.Qualification.PageLayoutEvidence.Save("workspace-readiness-loading", cut.Markup, "/todo/sync", "load-in-progress", ["status", "disabled-controls"], nameof(Loading_state_offers_wait_guidance_and_sync_does_not_link_to_itself));
        fixture.Client.LoadGate.SetResult(); await pending;
        fixture.Client.FailLoads=true; await cut.InvokeAsync(()=>todo.LoadSelectedAsync());
        Assert.Empty(cut.Find("[data-testid='workspace-readiness']").QuerySelectorAll("a[href='/todo/sync']"));
    }

    [Fact]
    public async Task Sync_page_uses_shared_source_and_policy_without_automatic_writes()
    {
        var (fixture, _) = await SetupAsync();
        var cut = Render<Habitica.WebApp.Pages.TodoSyncPage>();
        Assert.NotNull(cut.Find("[data-testid='todo-active-source']"));Assert.Empty(cut.FindAll("[data-testid='todo-source']"));
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
