using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Habitica.WebApp.Tests.Qualification;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class WorkspaceChecklistConversionPanelTests:BunitContext
{
    private async Task<(ChecklistConversionFixture Api,WorkspaceConversionController Controller)> Setup(string name="reuse")
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new WorkspaceTaskFixture();var api=new ChecklistConversionFixture(name);var commands=new WorkspaceTaskController(f.Session,api,f.Database);var todo=new TodoWorkspacePresentation(f.Session,api,f.Database,commands);
        Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton<IWorkspaceCloudClient>(api);Services.AddSingleton(commands);Services.AddSingleton(todo);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton(new WorkspaceOrganizationController(f.Session,api,new TodoOrganizationFixture.NativeApi(f),f.Database,TimeProvider.System));var controller=new WorkspaceConversionController(f.Session,api);Services.AddSingleton(controller);
        await commands.LoadAsync(true);await todo.RefreshSourcesAsync();await todo.LoadSelectedAsync();return(api,controller);
    }
    private static void Save(string name,string markup,string route="/todo/convert")=>PageLayoutEvidence.Save(name,markup,route,name,["form","confirmation","disabled-controls","navigation","error"],nameof(WorkspaceChecklistConversionPanelTests));
    [Fact]
    public async Task Ten_items_start_unclassified_exact_duplicate_selection_and_full_review_precede_save()
    {
        var(api,c)=await Setup();var cut=Render<TodoConversionPage>(p=>p.Add(x=>x.SourceTaskId,api.Source.Id));Assert.Equal(10,cut.FindAll("select[data-testid^='conversion-state-']").Count);Assert.Empty(api.Requests);Assert.All(c.Drafts,row=>Assert.Null(row.TargetTaskId));Assert.Contains(api.Source.Notes!,cut.Markup);
        Save("conversion-draft",cut.Markup,"/todo/convert/{SourceTaskId:guid}");
        var first=api.Template.Items[0];cut.Find($"[data-testid='conversion-state-{first.SourceItemId:D}']").Change("Active");cut.Find($"[data-testid='conversion-mode-{first.SourceItemId:D}']").Change("Reuse");cut.Find($"[data-testid='conversion-choose-{first.SourceItemId:D}']").Click();
        var candidates=cut.FindAll("button[data-testid^='conversion-target-']");Assert.Equal(8,candidates.Count);Assert.Null(c.Drafts[0].TargetTaskId);Save("conversion-exact-targets",cut.Markup);cut.Find($"[data-testid='conversion-target-{first.TargetTaskId:D}']").Click();Assert.Equal(first.TargetTaskId,c.Drafts[0].TargetTaskId);
        foreach(var item in api.Template.Items.Skip(1)){cut.Find($"[data-testid='conversion-state-{item.SourceItemId:D}']").Change(item.DesiredState.ToString());cut.Find($"[data-testid='conversion-mode-{item.SourceItemId:D}']").Change("Reuse");cut.Find($"[data-testid='conversion-choose-{item.SourceItemId:D}']").Click();cut.Find($"[data-testid='conversion-target-{item.TargetTaskId:D}']").Click();}
        cut.Find("[data-testid='conversion-prepare']").Click();Assert.Single(api.Requests);Assert.True(cut.Find("[data-testid='conversion-confirm']").HasAttribute("disabled"));Assert.Contains("Historical completion time remains uncertain",cut.Markup);Assert.Contains("Independent existing notes 9",cut.Markup);Save("conversion-full-review",cut.Markup);
        cut.Find("[data-testid='conversion-page-ack']").Change(true);cut.Find("[data-testid='conversion-confirm']").Click();cut.WaitForAssertion(()=>Assert.NotNull(c.LastReceipt));Assert.Single(api.Accepts);Assert.Contains("stored counterpart coverage: 0 of 10",cut.Markup);Assert.Contains("source retirement",cut.Markup,StringComparison.OrdinalIgnoreCase);Save("conversion-saved-gaps",cut.Markup);
    }
    [Fact]
    public async Task Navigation_preserves_unknown_original_review_and_receipt_recovery_without_new_preparation()
    {
        var(api,c)=await Setup("create");var cut=Render<WorkspaceChecklistConversionPanel>(p=>p.Add(x=>x.SourceTaskId,api.Source.Id));
        foreach(var row in c.Drafts){var original=api.Template.Items.Single(item=>item.SourceItemId==row.SourceItemId);row.Mode=original.Mode;row.DesiredState=original.DesiredState;row.Notes=original.Notes??"";}c.Difficulty=.1;c.Attribute=HabiticaTaskAttribute.Int;
        api.LosePreparation=true;cut.Find("[data-testid='conversion-prepare']").Click();Save("conversion-unresolved-preview",cut.Markup);cut.Find("[data-testid='conversion-retry-preview']").Click();cut.Find("[data-testid='conversion-page-ack']").Change(true);api.LoseAcceptance=true;cut.Find("[data-testid='conversion-confirm']").Click();var id=c.Preview!.PreparedCommandId;Save("conversion-unknown-acceptance",cut.Markup);cut.Dispose();
        var resumed=Render<TodoConversionPage>();Assert.Contains(id.ToString(),resumed.Markup);Assert.True(c.AcceptanceUnknown);Assert.Equal(2,api.Requests.Count);resumed.Find("[data-testid='conversion-recover']").Click();resumed.WaitForAssertion(()=>Assert.NotNull(c.LastReceipt));Assert.Equal(2,c.Status!.Targets.Count(row=>row.RequiresHistoricalMatch));Assert.Equal(9,api.State.Document.Tasks.Count);Save("conversion-partial-progress",resumed.Markup);
    }
    [Fact]
    public async Task Source_edits_show_current_and_retained_descriptions_and_refuse_target_redirection()
    {
        var(api,c)=await Setup();await c.LoadAsync(api.Source.Id);api.ChooseReuse(c);await c.PrepareAsync();c.AcknowledgePage(true);await c.ConfirmAsync();var original=api.Source;
        api.State=api.State with {Document=api.State.Document with {Revision=api.State.Document.Revision+1,Tasks=api.State.Document.Tasks.Select(task=>task.Id==original.Id?task with {Revision=api.State.Document.Revision+1,Notes="Current edited description"}:task).ToArray()}};
        var cut=Render<TodoConversionPage>(p=>p.Add(x=>x.SourceTaskId,original.Id));cut.Find("[data-testid='conversion-refresh']").Click();Assert.NotNull(cut.Find("[data-testid='conversion-source-changed']"));Assert.True(cut.Find("fieldset").HasAttribute("disabled"));Assert.Contains("Current edited description",cut.Markup);Assert.Contains(original.Notes!,cut.Markup);Assert.Single(api.Requests);Save("conversion-source-changed",cut.Markup);
    }
    [Fact]
    public async Task Retired_source_keeps_cleanup_history_entry_without_a_false_source_edit_warning()
    {
        var(api,c)=await Setup();var retirement=new SourceRetirementFixture();await retirement.PrepareSourceRetirementAsync(new(Guid.NewGuid(),retirement.Plan.ConversionId,retirement.Plan.Revision),default);for(var i=0;i<retirement.View!.VerificationSteps;i++)await retirement.VerifySourceRetirementAsync(retirement.View.CommandId,default);retirement.Save(SourceRetirementOutcome.OriginalReceipt);api.State=retirement.State;
        await Services.GetRequiredService<TodoWorkspacePresentation>().LoadSelectedAsync();var cut=Render<TodoConversionPage>(p=>p.Add(x=>x.SourceTaskId,retirement.Plan.Source.Id));Assert.Empty(cut.FindAll("[data-testid='conversion-source-changed']"));Assert.Equal($"/todo/retire/{retirement.Plan.ConversionId:D}",cut.Find("a[href^='/todo/retire/']").GetAttribute("href"));Assert.True(cut.Find("fieldset").HasAttribute("disabled"));Save("conversion-retired-source-history",cut.Markup,"/todo/convert/{SourceTaskId:guid}");
    }
    [Fact]
    public async Task Conversion_entrypoints_preserve_source_identity_on_capture_hierarchy_and_projects_routes()
    {
        var(api,_)=await Setup();var exact="/todo/convert/"+api.Source.Id.ToString("D");
        var tasks=Render<TodoTaskPanel>();Assert.Single(tasks.FindAll("a"),link=>link.GetAttribute("href")==exact);Save("conversion-capture-entry",tasks.Markup,"/tasks");
        var tree=Render<TodoTasksPage>();Assert.Single(tree.FindAll("a"),link=>link.GetAttribute("href")==exact);Save("conversion-tree-entry",tree.Markup,"/todo/tasks");
        var projects=Render<TodoProjectsPage>();Assert.Single(projects.FindAll("a"),link=>link.GetAttribute("href")=="/todo/convert");Assert.Empty(api.Requests);Save("conversion-project-entry",projects.Markup,"/todo/projects");
    }
    [Fact]
    public async Task Disconnected_conversion_explains_cloud_prerequisite_without_writes()
    {var(api,_)=await Setup();api.Connected=false;var cut=Render<TodoConversionPage>();Assert.Contains("Connect the cloud workspace",cut.Markup);Assert.Empty(api.Requests);Save("conversion-unavailable",cut.Markup);}
    [Fact]
    public async Task Destination_alias_selects_exact_existing_project_without_preparing_a_copy()
    {
        var(api,c)=await Setup();var project=new WorkspaceProject(Guid.NewGuid(),1,"Exact <codev> destination",Alias:new string('d',64));
        api.State=api.State with {Document=api.State.Document with {Projects=[project]}};await Services.GetRequiredService<TodoWorkspacePresentation>().LoadSelectedAsync();
        var cut=Render<WorkspaceChecklistConversionPanel>(p=>p.Add(x=>x.SourceTaskId,api.Source.Id));
        var option=cut.FindAll("[data-testid='plan-project'] option").Single(row=>row.TextContent.Contains("alias: "+project.Alias));cut.Find("[data-testid='plan-project']").Change(option.GetAttribute("value"));Assert.Equal(project.Id,c.ProjectId);Assert.Contains("&lt;codev&gt;",cut.Markup);
        Save("conversion-project-alias",cut.Markup);Assert.Empty(api.Requests);Assert.Empty(api.Accepts);Assert.Equal(project.Name,api.State.Document.Projects.Single().Name);
    }

}
