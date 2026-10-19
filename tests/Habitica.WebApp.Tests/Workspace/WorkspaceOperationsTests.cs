using Bunit;
using Habitica.Application.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class WorkspaceOperationsTests : BunitContext
{
    private WorkspaceTaskFixture Setup()
    {
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        Services.AddMudServices(); Services.AddSingleton<IJSRuntime>(f.Js); Services.AddSingleton<IAppSessionController>(f.Session);
        Services.AddSingleton<IWorkspaceCloudClient>(f.Client); Services.AddSingleton(f.Database);
        Services.AddSingleton(new WorkspaceDataController(f.Session, f.Client, f.Database));
        return f;
    }
    [Fact]
    public void Fresh_tab_does_not_infer_disconnection_or_offer_executor_changes()
    {
        var f=Setup();f.Client.Connected=false;
        var cut=Render<WorkspaceSettingsPanel>();
        Assert.Contains("unverified",cut.Find("[data-testid='workspace-unverified']").TextContent);
        Assert.True(cut.Find("[data-testid='workspace-connect']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='workspace-executor-enable']"));
        Assert.Equal(0,f.Session.WorkspaceConnectCalls);Assert.Equal(0,f.Client.ConnectionStatusReads);
        WorkspaceLayoutEvidence.Save("workspace-fresh-tab",cut.Markup);
    }

    [Fact]
    public void Explicit_recovery_rereads_retained_executor_state_without_enabling_it()
    {
        var f = Setup(); f.Client.Connected = false; f.Client.ConnectionStatusVerified = false;
        f.Session.WorkspaceConnectAction = () =>
        {
            f.Client.Connected = true;
            return Task.FromResult(new WorkspaceActionResult(true, "Tab access renewed"));
        };
        f.Client.ConnectionStatusAction = () =>
        {
            f.Client.ConnectionStatusVerified = true; f.Client.ExecutorEnabled = true;
            return Task.FromResult(new WorkspaceActionResult(true, "Existing executor verified"));
        };
        var cut = Render<WorkspaceSettingsPanel>();
        cut.Find("[data-testid='workspace-consent']").Change(true);
        cut.Find("[data-testid='workspace-connect']").Click();
        Assert.Equal(1, f.Session.WorkspaceConnectCalls); Assert.Equal(1, f.Client.ConnectionStatusReads);
        Assert.Contains("enabled", cut.Find("[data-testid='workspace-executor-state']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='workspace-executor-pause']"));
        Assert.Empty(cut.FindAll("[data-testid='workspace-executor-enable']"));
        Assert.Empty(f.Client.AcceptedIds); Assert.Empty(f.Client.PreparationIds);
        WorkspaceLayoutEvidence.Save("workspace-recovered", cut.Markup);
    }

    [Fact]
    public void Failed_server_refresh_hides_executor_controls_instead_of_claiming_paused_state()
    {
        var f=Setup();f.Client.ConnectionStatusAction=()=>
        {f.Client.ConnectionStatusVerified=false;return Task.FromResult(new WorkspaceActionResult(false,"Synthetic server status unavailable"));};
        var cut=Render<WorkspaceSettingsPanel>();
        cut.Find("[data-testid='workspace-check-status']").Click();
        Assert.NotNull(cut.Find("[data-testid='workspace-executor-unverified']"));
        Assert.Empty(cut.FindAll("[data-testid='workspace-executor-enable']"));
        Assert.Empty(cut.FindAll("[data-testid='workspace-executor-pause']"));
        Assert.Equal(1,f.Client.ConnectionStatusReads);Assert.Equal(0,f.Client.StatusReads);
        Assert.Empty(f.Client.AcceptedIds);Assert.Empty(f.Client.PreparationIds);
        WorkspaceLayoutEvidence.Save("workspace-status-unavailable",cut.Markup);
    }

    [Fact]
    public async Task Account_replacement_hides_a_late_workspace_action_message()
    {
        var f=Setup();f.Client.Connected=false;
        var response=new TaskCompletionSource<WorkspaceActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Session.WorkspaceConnectAction=()=>response.Task;
        var cut=Render<WorkspaceSettingsPanel>();
        cut.Find("[data-testid='workspace-consent']").Change(true);
        var action=cut.InvokeAsync(()=>cut.Find("[data-testid='workspace-connect']").Click());await Task.Yield();
        await cut.InvokeAsync(()=>f.Session.SetState(f.Session.State with {UserId=Guid.NewGuid().ToString()}));
        response.SetResult(new(false,"Private old workspace connection"));await action.WaitAsync(TimeSpan.FromSeconds(3));
        cut.WaitForAssertion(()=>Assert.DoesNotContain("Private old workspace connection",cut.Markup));
        Assert.True(cut.Find("[data-testid='workspace-connect']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Account_replacement_during_recovery_status_cannot_return_connection_success()
    {
        var f = Setup();
        f.Session.WorkspaceConnectAction = () => Task.FromResult(new WorkspaceActionResult(true, "Private connection"));
        var pending = new TaskCompletionSource<WorkspaceActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Client.ConnectionStatusAction = () => pending.Task;
        var controller = new WorkspaceDataController(f.Session, f.Client, f.Database);
        var recovery = controller.ConnectAsync(true);
        f.Session.SetState(f.Session.State with { UserId = Guid.NewGuid().ToString() });
        pending.SetResult(new(true, "Private status"));
        var result = await recovery.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(result.Succeeded); Assert.DoesNotContain("Private", result.Message);
    }

    [Fact]
    public void StatusIsManualAndNeverPreparesOrExecutesAnAction()
    {
        var f = Setup(); f.Client.StatusAction = () => Task.FromResult(new WorkspaceStatusResult(true, "Checked", new(DateTimeOffset.UtcNow, 3, 2, 1, 4, 1, DateTimeOffset.UtcNow.AddHours(-1))));
        var cut = Render<WorkspaceSettingsPanel>(); Assert.Equal(0, f.Client.StatusReads);
        cut.Find("[data-testid='workspace-check-status']").Click();
        var text = cut.Find("[data-testid='workspace-operations']").TextContent;
        Assert.Contains("Pending: 3", text); Assert.Contains("Uncertain: 1", text); Assert.Contains("never retries", text);
        Assert.Empty(f.Client.PreparationIds); Assert.Empty(f.Client.AcceptedIds);
        WorkspaceLayoutEvidence.Save("workspace-operational-status", cut.Markup);
        f.Session.SetState(f.Session.State with { UserId = Guid.NewGuid().ToString() }); cut.Render();
        Assert.Empty(cut.FindAll("[data-testid='workspace-operations']"));
    }
    [Fact]
    public async Task LateStatusFromAnotherAccountIsDiscarded()
    {
        var f = Setup(); var pending = new TaskCompletionSource<WorkspaceStatusResult>(); f.Client.StatusAction = () => pending.Task;
        var controller = new WorkspaceDataController(f.Session, f.Client, f.Database);
        var request = controller.ReadOperationalStatusAsync(); f.Session.SetState(f.Session.State with { UserId = Guid.NewGuid().ToString() });
        pending.SetResult(new(true, "Private status", new(DateTimeOffset.UtcNow, 7, 0, 1, 0, 0, null)));
        var result = await request; Assert.False(result.Succeeded); Assert.Null(result.Status);
    }
    [Fact]
    public void UnavailableStatusDoesNotOfferAnAutomaticRetry()
    {
        var f = Setup(); var cut = Render<WorkspaceSettingsPanel>();
        cut.Find("[data-testid='workspace-check-status']").Click(); Assert.Empty(cut.FindAll("[data-testid='workspace-operations']"));
        Assert.Equal(1, f.Client.StatusReads); Assert.Equal(1, f.Client.ConnectionStatusReads); Assert.Empty(f.Client.AcceptedIds);
    }
}
