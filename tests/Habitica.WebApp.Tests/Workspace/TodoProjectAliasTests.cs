using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoProjectAliasTests:BunitContext
{
    private async Task<TodoOrganizationFixture> Setup(WorkspaceDocument document)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new TodoOrganizationFixture();await f.Load(document:document);
        Services.AddSingleton<IAppSessionController>(f.Task.Session);Services.AddSingleton<IWorkspaceCloudClient>(f.Task.Client);Services.AddSingleton(f.Task.Database);Services.AddSingleton(f.Task.Controller);Services.AddSingleton(f.Organization);Services.AddSingleton(f.Todo);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton(TimeProvider.System);return f;
    }
    private static void Save(string name,string markup,string state,string test,string route="/todo/projects")=>Qualification.PageLayoutEvidence.Save(name,markup,route,state,["form","actions","confirmation","disabled-controls","error","navigation"],test);
    [Fact]
    public async Task Create_alias_and_explicit_rename_clear_keep_separate_original_receipts()
    {
        using var f=await Setup(new(){Id=Guid.NewGuid()});var cut=Render<TodoOrganizationPanel>();cut.Find("[data-testid='organization-name']").Input("Companion Development <codev>");cut.Find("[data-testid='organization-alias']").Input(" CODEV ");cut.Find("form").Submit();
        var preview=f.Organization.Preview!;Assert.Equal("codev",preview.Snapshot.Alias);Assert.Equal("Companion Development <codev>",preview.Snapshot.Name);Assert.Contains("&lt;codev&gt;",cut.Markup);Assert.Equal("Alias: codev",cut.Find("[data-testid='organization-alias-review']").TextContent);
        Save("project-alias-create-review",cut.Markup,"normalized-alias-with-exact-escaped-name",nameof(Create_alias_and_explicit_rename_clear_keep_separate_original_receipts));cut.Find("[data-testid='organization-confirm']").Click();var project=f.Organization.LastReceipt!.EntityId;
        cut.Find($"[data-testid='organization-edit-{project:D}']").Click();cut.Find("[data-testid='organization-name']").Input("Separate name draft");cut.Find("[data-testid='organization-alias']").Input("renamed-42");cut.Find("[data-testid='organization-alias-form']").Submit();
        var alias=f.Organization.Preview!;Assert.Null(alias.Command.Patch!.Name);Assert.Equal("Companion Development <codev>",alias.Snapshot.Name);Assert.Equal(project,alias.Command.EntityId);Assert.NotEqual(preview.PreparedCommandId,alias.PreparedCommandId);
        cut.Find("[data-testid='organization-confirm']").Click();Assert.Equal("Companion Development <codev>",f.Todo.Snapshot!.Document.Projects.Single().Name);
        cut.Find($"[data-testid='organization-edit-{project:D}']").Click();cut.Find("[data-testid='organization-name']").Input("Companion Development");cut.Find("form").Submit();Assert.Null(f.Organization.Preview!.Command.Patch!.Alias);cut.Find("[data-testid='organization-confirm']").Click();
        cut.Find($"[data-testid='organization-edit-{project:D}']").Click();cut.Find("[data-testid='organization-alias']").Input("");cut.Find("[data-testid='organization-alias-form']").Submit();Assert.True(f.Organization.Preview!.Command.Patch!.ClearAlias);Assert.Contains("Release the alias",cut.Markup);cut.Find("[data-testid='organization-confirm']").Click();
        Assert.Null(f.Todo.Snapshot!.Document.Projects.Single().Alias);Assert.Equal("Companion Development",f.Todo.Snapshot.Document.Projects.Single().Name);Assert.Equal(4,f.Todo.Snapshot.Document.OrganizationHistory!.Count);Assert.Empty(f.Todo.Snapshot.Commands);Assert.Empty(f.Task.Client.AcceptedIds);
    }
    [Fact]
    public async Task Alias_repair_recovers_original_preparation_and_unknown_acceptance_without_name_edits()
    {
        var project=new WorkspaceProject(Guid.NewGuid(),1,new string('W',180)+" <codev>",Alias:new string('a',64));using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Projects=[project]});var cut=Render<TodoOrganizationPanel>();cut.Find($"[data-testid='organization-edit-{project.Id:D}']").Click();
        Save("project-alias-editor",cut.Markup,"long-name-and-maximum-alias-separate-editor",nameof(Alias_repair_recovers_original_preparation_and_unknown_acceptance_without_name_edits));cut.Find("[data-testid='organization-alias']").Input("codev");f.Api.LosePreparation=true;cut.Find("[data-testid='organization-alias-form']").Submit();Assert.True(f.Organization.HasPendingIntent);cut.Find("[data-testid='organization-retry']").Click();var id=f.Organization.Preview!.PreparedCommandId;
        f.Api.LoseAcceptance=true;cut.Find("[data-testid='organization-confirm']").Click();Assert.True(f.Organization.AcceptanceUnknown);Save("project-alias-unknown",cut.Markup,"original-unknown-alias-command",nameof(Alias_repair_recovers_original_preparation_and_unknown_acceptance_without_name_edits));cut.Find("[data-testid='organization-recover']").Click();Assert.Equal(id,f.Organization.LastReceipt!.CommandId);Assert.Single(f.Api.Acceptances);Assert.Equal(project.Name,f.Todo.Snapshot!.Document.Projects.Single().Name);Assert.Equal("codev",f.Todo.Snapshot.Document.Projects.Single().Alias);
    }
    [Fact]
    public async Task Invalid_duplicate_and_stale_alias_drafts_refuse_then_source_switch_resets_input()
    {
        var a=new WorkspaceProject(Guid.NewGuid(),1,"Same",Alias:"taken");var b=new WorkspaceProject(Guid.NewGuid(),1,"Same");using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Projects=[a,b]});var cut=Render<TodoOrganizationPanel>();cut.Find($"[data-testid='organization-edit-{b.Id:D}']").Click();
        foreach(var value in new[]{"bad--alias","TAKEN"}){cut.Find("[data-testid='organization-alias']").Input(value);cut.Find("[data-testid='organization-alias-form']").Submit();Assert.Null(f.Organization.Preview);Assert.Empty(f.Api.Acceptances);}
        cut.Find("[data-testid='organization-alias']").Input("stale");f.Task.Client.State=f.Task.Client.State with{Document=f.Task.Client.State.Document with{Revision=11},StoreVersion=11};await f.Todo.LoadSelectedAsync();cut.Find("[data-testid='organization-alias-form']").Submit();Assert.Null(f.Organization.Preview);Assert.Contains("Reload",cut.Markup);
        await f.Todo.CreateBrowserAsync();Assert.Equal("",cut.Find("[data-testid='organization-alias']").GetAttribute("value"));Assert.Empty(cut.FindAll("[data-testid='organization-alias-form']"));Assert.Empty(f.Api.Acceptances);
    }
    [Fact]
    public async Task Pickers_show_aliases_and_preserve_exact_off_page_and_archived_selection()
    {
        var selected=new WorkspaceProject(Guid.NewGuid(),1,"Same project",Alias:"codev");var archived=new WorkspaceProject(Guid.NewGuid(),1,"<Archived>",IsArchived:true,Alias:new string('r',64));var projects=Enumerable.Range(0,24).Select(i=>new WorkspaceProject(Guid.NewGuid(),1,"Same project")).Append(selected).Append(archived).ToArray();using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Projects=projects});Guid? changed=null;
        var cut=Render<TodoPlacementPicker>(p=>p.Add(x=>x.Value,selected.Id).Add(x=>x.ValueChanged,value=>changed=value));Assert.Contains("alias: codev",cut.Find($"option[value='{selected.Id:D}']").TextContent);Assert.Contains("retained selection",cut.Find($"option[value='{selected.Id:D}']").TextContent);Assert.Contains("Alias: codev",cut.Markup);
        cut.FindAll("button").Single(row=>row.TextContent.Trim()=="Next").Click();var option=cut.FindAll("option").Single(row=>row.TextContent.Contains("alias: codev"));cut.Find("select").Change(option.GetAttribute("value"));Assert.Equal(selected.Id,changed);
        cut.Render(p=>p.Add(x=>x.Value,archived.Id));Assert.Contains("alias: "+new string('r',64),cut.Find($"option[value='{archived.Id:D}']").TextContent);Assert.Contains("&lt;Archived&gt;",cut.Markup);Assert.Equal(archived.Id.ToString(),cut.Find("select").GetAttribute("value"));
        Save("project-alias-retained-picker",cut.Markup,"retained-archived-alias-and-duplicate-name-ids",nameof(Pickers_show_aliases_and_preserve_exact_off_page_and_archived_selection));Assert.Empty(f.Api.Requests);Assert.Empty(f.Task.Client.AcceptedIds);
    }
    [Fact]
    public async Task Alias_label_planning_selects_exact_ID_without_creating_metadata_or_game_effects()
    {
        var now=DateTimeOffset.UtcNow;var a=new WorkspaceProject(Guid.NewGuid(),1,"Duplicate",Alias:"codev");var b=new WorkspaceProject(Guid.NewGuid(),1,"Duplicate",Alias:"another");var task=new CompanionTask(Guid.NewGuid(),1,"Plan by project alias",CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,now,now);using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Projects=[a,b],Tasks=[task]});TodoPlacementReview? review=null;
        var cut=Render<TodoPlanEditor>(p=>p.Add(x=>x.TaskId,task.Id).Add(x=>x.Reviewed,value=>review=value));var option=cut.FindAll("[data-testid='plan-project'] option").Single(row=>row.TextContent.Contains("alias: codev"));Assert.Contains(a.Id.ToString(),option.TextContent);cut.Find("[data-testid='plan-project']").Change(option.GetAttribute("value"));cut.Find("[data-testid='plan-review']").Click();Assert.Equal(a.Id,review!.Planning.ProjectId);
        Save("project-alias-planning",cut.Markup,"alias-label-resolved-to-exact-native-placement",nameof(Alias_label_planning_selects_exact_ID_without_creating_metadata_or_game_effects),"/todo/plan");Assert.Empty(f.Api.Requests);Assert.Empty(f.Task.Client.AcceptedIds);
    }
}
