using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
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
        WorkspaceLayoutEvidence.Save("canonical-preview", cut.Markup);
        cut.Find("[data-testid='canonical-confirm']").Click();
        Assert.Single(f.Controller.Snapshot!.Commands);
        Assert.Contains("Saved in Companion: Yes", cut.Find("[data-testid='canonical-receipt']").TextContent);
        Assert.Contains("pending", cut.Find("[data-testid='canonical-receipt']").TextContent);
        Assert.Contains("Recent command history", cut.Markup);
        f.Session.SetState(f.Session.State with { IsAuthenticated = false });
        cut.WaitForAssertion(() => Assert.DoesNotContain("A reviewed local task", cut.Markup));
        Assert.Contains("Sign in to manage", cut.Markup);
    }
    [Theory]
    [InlineData("CreateDaily", "Daily")]
    [InlineData("CreateHabit", "Habit")]
    public async Task NativeCreationAndArchiveShowReviewAndRetainHistory(string operation, string kind)
    {
        var f = new WorkspaceTaskFixture(); Services.AddMudServices();
        Services.AddSingleton<IJSRuntime>(f.Js); Services.AddSingleton<IAppSessionController>(f.Session);
        Services.AddSingleton<IWorkspaceCloudClient>(f.Client); Services.AddSingleton(f.Database); Services.AddSingleton(f.Controller);
        var cut = Render<CompanionTasksPanel>();
        cut.Find("[data-testid='canonical-new-workspace']").Click();
        cut.Find("[data-testid='canonical-operation']").Change(operation);
        cut.Find("[data-testid='canonical-text']").Change("Disposable native task");
        if (kind == "Daily") cut.Find("[data-testid='daily-start']").Change("2026-10-19");
        Assert.Empty(cut.FindAll("[data-testid='canonical-project']"));
        cut.Find("[data-testid='canonical-preview']").Click();
        Assert.Empty(f.Controller.Snapshot!.Document.Tasks);
        Assert.Contains(kind, cut.Find(".companion-command-preview").TextContent);
        if (kind == "Daily") { Assert.Contains("Daily schedule", cut.Find(".companion-command-preview").TextContent); WorkspaceLayoutEvidence.Save("canonical-daily-preview", cut.Markup); }
        cut.Find("[data-testid='canonical-confirm']").Click();
        var task = Assert.Single(f.Controller.Snapshot!.Document.Tasks); Assert.Equal(kind, task.Kind.ToString());
        if (kind is "Habit" or "Daily")
        {
            Assert.Equal(ProjectionCommandState.Pending, f.Controller.Snapshot.Commands.Single().ProjectionState);
            await f.SeedLegacyLocalAsync("Retained native " + kind, kind: Enum.Parse<CompanionTaskKind>(kind));
            task = f.Controller.Snapshot!.Document.Tasks.Single();
            cut.Dispose(); cut = Render<CompanionTasksPanel>();
        }
        cut.Find($"[data-testid='canonical-task-{task.Id:D}']").Click();
        cut.Find("[data-testid='canonical-operation']").Change("DeleteTask");
        cut.Find("[data-testid='canonical-preview']").Click();
        Assert.False(Assert.Single(f.Controller.Snapshot.Document.Tasks).IsArchived);
        Assert.Contains("history remain in exports", cut.Find(".companion-command-preview").TextContent);
        WorkspaceLayoutEvidence.Save("canonical-archive-preview", cut.Markup);
        cut.Find("[data-testid='canonical-confirm']").Click();
        Assert.True(Assert.Single(f.Controller.Snapshot.Document.Tasks).IsArchived);
        Assert.Empty(cut.FindAll($"[data-testid='canonical-task-{task.Id:D}']"));
        Assert.Equal(2, f.Controller.Snapshot.Commands.Count);
        Assert.Contains("0 listed tasks · 1 archived", cut.Markup);
        Assert.Equal("CreateTodo", cut.Find("[data-testid='canonical-operation']").GetAttribute("value"));
        cut.Find("[data-testid='canonical-operation']").Change("CreateDaily");
        Assert.Equal("", cut.Find("[data-testid='daily-start']").GetAttribute("value"));
    }

}
