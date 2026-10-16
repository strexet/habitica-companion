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
        Assert.Equal(1, f.Client.StatusReads); Assert.Empty(f.Client.AcceptedIds);
    }
}
