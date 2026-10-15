using Bunit;
using Habitica.Application.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class CompanionTasksPanelTests : BunitContext
{
    [Fact]
    public void BrowserTaskRequiresAVisibleReviewThenShowsSavedAndProjectedStatesSeparately()
    {
        var f = new WorkspaceTaskFixture(); Services.AddMudServices();
        Services.AddSingleton<IJSRuntime>(f.Js); Services.AddSingleton<IAppSessionController>(f.Session);
        Services.AddSingleton<IWorkspaceCloudClient>(f.Client); Services.AddSingleton(f.Database); Services.AddSingleton(f.Controller);
        var cut = Render<CompanionTasksPanel>();
        cut.Find("[data-testid='canonical-new-workspace']").Click();
        cut.Find("[data-testid='canonical-text']").Change("A reviewed local task");
        cut.Find("[data-testid='canonical-notes']").Change("<script>untrusted note</script>");
        cut.Find("[data-testid='canonical-preview']").Click();
        Assert.Empty(f.Controller.Snapshot!.Commands); Assert.Contains("Review before saving", cut.Markup);
        Assert.Empty(cut.FindAll("script"));
        Assert.Contains("<script>untrusted note</script>", cut.Find(".companion-command-preview").TextContent);
        cut.Find("[data-testid='canonical-confirm']").Click();
        Assert.Single(f.Controller.Snapshot!.Commands);
        Assert.Contains("Saved in Companion: Yes", cut.Find("[data-testid='canonical-receipt']").TextContent);
        Assert.Contains("notRequested", cut.Find("[data-testid='canonical-receipt']").TextContent);
        Assert.Contains("Recent command history", cut.Markup);
        f.Session.SetState(f.Session.State with { IsAuthenticated = false });
        cut.WaitForAssertion(() => Assert.DoesNotContain("A reviewed local task", cut.Markup));
        Assert.Contains("Sign in to manage", cut.Markup);
    }
}
