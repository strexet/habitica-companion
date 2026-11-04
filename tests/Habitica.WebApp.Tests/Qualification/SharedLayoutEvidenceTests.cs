using Bunit;
using Habitica.WebApp.Components.Navigation;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Qualification;

public sealed class SharedLayoutEvidenceTests : BunitContext
{
    protected override void Dispose(bool disposing)
    {
        if (disposing) { DisposeAsync().AsTask().GetAwaiter().GetResult(); return; }
        base.Dispose(disposing);
    }

    [Theory]
    [InlineData(Habitica.Domain.Workspace.CompanionTaskKind.Todo,"/todo/tasks","disabled-task-frozen")]
    [InlineData(Habitica.Domain.Workspace.CompanionTaskKind.Daily,"/todo/dailies","disabled-daily-frozen")]
    public async Task Frozen_capture_retains_readable_values_and_native_disabled_authority(Habitica.Domain.Workspace.CompanionTaskKind kind,string route,string name)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();
        var f=new Habitica.WebApp.Tests.Workspace.WorkspaceTaskFixture();await f.Controller.CreateLocalAsync();var original=f.Controller.Snapshot!;
        var frozen=original with {StoreVersion=original.StoreVersion+1,Authority=new(Habitica.Domain.Workspace.WorkspaceAuthorityMode.Frozen,Guid.NewGuid(),null)};
        Assert.True(await f.Database.ForScope(Habitica.WebApp.Tests.Workspace.WorkspaceTaskFixture.User).TryCommitAsync(original.StoreVersion,frozen,default));
        using var todo=new TodoWorkspacePresentation(f.Session,f.Client,f.Database,f.Controller);await todo.LoadSelectedAsync();
        var drafts=new TodoTaskDraftStore(f.Session);var draft=drafts.Get(todo.Selection!,kind,null);draft.Text="Retained task title";draft.Notes="Retained disabled notes";
        Services.AddSingleton(todo);Services.AddSingleton(drafts);
        var cut=Render<Habitica.WebApp.Components.TodoTaskPanel>(p=>p.Add(x=>x.Kind,kind).Add(x=>x.ShowTaskList,false));
        Assert.True(cut.Find("fieldset").HasAttribute("disabled"));Assert.Equal("Retained task title",cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        Assert.Contains("read only",cut.Find("[data-testid='workspace-readiness']").TextContent);
        cut.Find("details.todo-advanced").SetAttribute("open","");
        var capture=cut.Find("[data-testid='todo-direct-panel']").OuterHtml;
        Assert.True(new AngleSharp.Html.Parser.HtmlParser().ParseDocument(capture).QuerySelector("details.todo-advanced")!.HasAttribute("open"));
        Assert.Empty(f.Client.AcceptedIds);Assert.Empty(f.Client.PreparationIds);
        PageLayoutEvidence.Save(name,capture,route,"frozen-native-fields-with-advanced-disclosure-open",["form","disabled-controls","recovery"],nameof(Frozen_capture_retains_readable_values_and_native_disabled_authority));
    }

    [Fact]
    public void Authenticated_navigation_retains_real_routes_and_drawer_surface()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty with { IsAuthenticated = true }));
        var cut = Render<MudDrawer>(p => p.Add(x => x.Open, true).Add(x => x.Variant, DrawerVariant.Persistent)
            .Add(x => x.Class, "app-drawer").AddChildContent<AppNavMenu>());
        Assert.Contains(cut.FindAll("a"), a => a.GetAttribute("href") == "/tasks");
        PageLayoutEvidence.Save("shared-navigation", cut.Markup, "shared-navigation", "authenticated", ["navigation"], nameof(Authenticated_navigation_retains_real_routes_and_drawer_surface));
    }
}
