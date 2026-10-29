using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoHierarchyPanelTests : BunitContext
{
    private static readonly DateTimeOffset Now=new(2026,10,1,0,0,0,TimeSpan.Zero);
    private static CompanionTask Task(string text,Guid? parent=null)=>new(Guid.NewGuid(),1,text,CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,Now,Now,ParentTaskId:parent);
    private static WorkspaceDocument Doc(params CompanionTask[] tasks)=>new(){Id=Guid.NewGuid(),Revision=10,Tasks=tasks};
    private async Task<(WorkspaceTaskFixture F,TodoWorkspacePresentation Todo)> Setup(WorkspaceDocument document,bool cloud=true)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new WorkspaceTaskFixture();f.Client.Connected=true;Assert.Empty(WorkspaceRules.Validate(document));f.Client.State=new(document,10,[],[]);
        if(cloud)await f.Controller.LoadAsync(true);else { f.Client.Connected=false;Assert.True(await f.Database.ForScope(WorkspaceTaskFixture.User).TryCommitAsync(0,new(document,10,[],[]),default));await f.Controller.LoadAsync(false,document.Id); }
        Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton<IWorkspaceCloudClient>(f.Client);Services.AddSingleton(f.Database);Services.AddSingleton(f.Controller);
        var todo=new TodoWorkspacePresentation(f.Session,f.Client,f.Database,f.Controller);Services.AddSingleton(todo);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton(TimeProvider.System);return(f,todo);
    }
    private static void Save(string name,string markup,string state,string test,string route="/todo/tasks")=>Qualification.PageLayoutEvidence.Save(name,markup,route,state,["form","actions","confirmation","disabled-controls","error","navigation"],test);
    private static void Button(IRenderedComponent<TodoHierarchyPanel> cut,string text)=>cut.FindAll("button").Single(button=>button.TextContent.Trim()==text).Click();
    [Fact]
    public async Task Retained_branches_expand_collapse_and_page_with_exact_parent_context_and_no_writes()
    {
        var root=Task("Same name");var archived=Task("Archived parent",root.Id) with {IsArchived=true};var child=Task("Same name",archived.Id);var other=Task("Other root");var(f,_)=await Setup(Doc(root,archived,child,other));
        var before=WorkspaceJournalJson.Serialize(f.Client.State);var cut=Render<TodoHierarchyPanel>();Assert.Equal(2,cut.FindAll("[data-testid^='tree-row-']").Count);
        cut.Find($"[data-testid='tree-expand-{root.Id:D}']").Click();Assert.Single(cut.FindAll("[data-testid^='tree-row-']"));Assert.Contains("archived",cut.Markup);
        cut.Find($"[data-testid='tree-expand-{archived.Id:D}']").Click();Assert.Contains("retained context",cut.Markup);Assert.Contains(child.Id.ToString(),cut.Markup);Assert.True(cut.FindAll("[data-testid='tree-context'] button").All(button=>button.HasAttribute("disabled")));
        cut.Find("[data-testid='tree-parent']").Click();cut.Find("[data-testid='tree-subtree']").Click();Assert.Equal(3,cut.FindAll("[data-testid^='tree-row-']").Count);Assert.Contains("level 2",cut.Markup);
        Save("todo-tree-retained",cut.Markup,"expanded-archived-parent-context",nameof(Retained_branches_expand_collapse_and_page_with_exact_parent_context_and_no_writes));
        cut.Find("[data-testid='tree-roots']").Click();Assert.Equal(2,cut.FindAll("[data-testid^='tree-row-']").Count);cut.Find($"[data-testid='tree-expand-{root.Id:D}']").Click();cut.Find("[data-testid='tree-subtree']").Click();cut.Find("[data-testid='tree-subtree']").Click();Assert.Single(cut.FindAll("[data-testid^='tree-row-']"));cut.Find("[data-testid='tree-roots']").Click();Assert.Equal(2,cut.FindAll("[data-testid^='tree-row-']").Count);Assert.Equal(before,WorkspaceJournalJson.Serialize(f.Client.State));Assert.Empty(f.Client.PreparationIds);
        Assert.Contains(JSInterop.Invocations,row=>row.Identifier=="Blazor._internal.domWrapper.focus");
    }
    [Fact]
    public async Task Two_reviewed_create_and_attach_steps_recover_original_receipts_without_duplicate_Todos()
    {
        var root=Task(new string('W',120)+" Parent");var(f,_)=await Setup(Doc(root));var cut=Render<TodoHierarchyPanel>();
        for(var level=0;level<2;level++)
        {
            cut.Find($"[data-testid='tree-add-{root.Id:D}']").Click();var focusBeforeTyping=JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus");cut.Find("[data-testid='child-title']").Input("Independent subtask "+level);Assert.Equal(focusBeforeTyping,JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus"));cut.Find("[data-testid='child-notes']").Input("Child notes");cut.Find("[data-testid='child-deadline']").Change("2026-11-01");
            cut.Find("[data-testid='child-create']").Closest("form")!.Submit();var createdPreview=f.Controller.Preview!;Assert.Equal(WorkspaceOperation.CreateTodo,createdPreview.Command.Operation);Assert.True(createdPreview.MayAffectHabitica);Assert.Null(createdPreview.Command.Patch!.Relationships);
            Save("todo-tree-create-review",cut.Markup,"long-parent-independent-create-review",nameof(Two_reviewed_create_and_attach_steps_recover_original_receipts_without_duplicate_Todos));
            f.Client.LoseAcceptance=true;cut.Find("[data-testid='todo-confirm']").Click();Assert.True(f.Controller.AcceptanceUnknown);cut.Find("[data-testid='todo-recover']").Click();cut.WaitForAssertion(()=>Assert.NotNull(cut.Find("[data-testid='child-created']")));
            var childId=f.Controller.LastReceipt!.TaskId;Assert.Null(f.Controller.Snapshot!.Document.Tasks.Single(task=>task.Id==childId).ParentTaskId);
            cut.Find("[data-testid='child-attach']").Click();var attach=f.Controller.Preview!;Assert.Equal(childId,attach.Command.TaskId);Assert.Equal(root.Id,attach.Command.Patch!.Relationships!.ParentTaskId);Assert.Contains("Project membership",cut.Find("[data-testid='todo-review']").TextContent);
            Save("todo-tree-attach-review",cut.Markup,"exact-attachment-review",nameof(Two_reviewed_create_and_attach_steps_recover_original_receipts_without_duplicate_Todos));
            f.Client.LoseAcceptance=true;cut.Find("[data-testid='todo-confirm']").Click();Save("todo-tree-unknown",cut.Markup,"uncertain-attachment-original-receipt",nameof(Two_reviewed_create_and_attach_steps_recover_original_receipts_without_duplicate_Todos));cut.Find("[data-testid='todo-recover']").Click();
            cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid='child-created']")));root=f.Controller.Snapshot!.Document.Tasks.Single(task=>task.Id==childId);Assert.Equal("Child notes",root.Notes);Assert.Equal(new DateOnly(2026,11,1),root.Deadline);Assert.Null(root.ProjectId);
        }
        Assert.Equal(3,f.Controller.Snapshot!.Document.Tasks.Count);Assert.Equal(4,f.Client.AcceptedIds.Count);Assert.All(f.Controller.Snapshot.Commands.Where(row=>row.Command.Operation==WorkspaceOperation.CreateTodo),row=>Assert.Equal(new[]{ProjectionEffectKind.Create},row.Effects));Assert.All(f.Controller.Snapshot.Commands.Where(row=>row.Command.Operation==WorkspaceOperation.RelateTodo),row=>Assert.Empty(row.Effects));Assert.Empty(f.Controller.Snapshot.Document.Completions);
    }
    [Fact]
    public async Task Lost_create_and_attachment_previews_keep_original_request_bindings_for_the_second_step()
    {
        var root=Task("Parent");var(f,_)=await Setup(Doc(root));var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-add-{root.Id:D}']").Click();cut.Find("[data-testid='child-title']").Input("Recovered child");
        f.Client.LosePreparation=true;cut.Find("[data-testid='child-create']").Closest("form")!.Submit();var createRequest=f.Controller.OriginalRequestId;Assert.True(f.Controller.HasPendingIntent);Assert.Null(f.Controller.Preview);
        Button(cut,"Retry original preview");Assert.Equal(createRequest,f.Controller.OriginalRequestId);var command=f.Controller.Preview!.PreparedCommandId;cut.Find("[data-testid='todo-confirm']").Click();cut.WaitForAssertion(()=>Assert.NotNull(cut.Find("[data-testid='child-created']")));
        Assert.Equal(command,f.Controller.LastReceipt!.CommandId);f.Client.LosePreparation=true;cut.Find("[data-testid='child-attach']").Click();var attachRequest=f.Controller.OriginalRequestId;Assert.NotEqual(createRequest,attachRequest);Assert.Null(f.Controller.Preview);
        Button(cut,"Retry original preview");Assert.Equal(attachRequest,f.Controller.OriginalRequestId);cut.Find("[data-testid='todo-confirm']").Click();cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid='child-created']")));
        Assert.Equal(2,f.Controller.Snapshot!.Document.Tasks.Count);Assert.Equal(root.Id,f.Controller.Snapshot.Document.Tasks.Single(task=>task.Id!=root.Id).ParentTaskId);Assert.Equal(2,f.Client.AcceptedIds.Count);
    }

    [Fact]
    public async Task Browser_local_two_step_creation_recovers_both_original_commits()
    {
        var root=Task("Local parent");var(f,_)=await Setup(Doc(root),false);var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-add-{root.Id:D}']").Click();cut.Find("[data-testid='child-title']").Input("Local child");cut.Find("[data-testid='child-create']").Closest("form")!.Submit();
        f.Js.LoseCommitAcknowledgement=true;cut.Find("[data-testid='todo-confirm']").Click();Assert.True(f.Controller.AcceptanceUnknown);cut.Find("[data-testid='todo-recover']").Click();cut.WaitForAssertion(()=>Assert.NotNull(cut.Find("[data-testid='child-created']")));
        cut.Find("[data-testid='child-attach']").Click();f.Js.LoseCommitAcknowledgement=true;cut.Find("[data-testid='todo-confirm']").Click();Assert.True(f.Controller.AcceptanceUnknown);cut.Find("[data-testid='todo-recover']").Click();cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid='child-created']")));
        Assert.Equal(2,f.Controller.Snapshot!.Commands.Count);Assert.Equal(root.Id,f.Controller.Snapshot.Document.Tasks.Single(task=>task.Id!=root.Id).ParentTaskId);Assert.Empty(f.Client.AcceptedIds);
    }
    [Fact]
    public async Task Parent_becoming_archived_after_creation_keeps_the_exact_saved_child_without_recreation()
    {
        var root=Task("Parent");var(f,todo)=await Setup(Doc(root));var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-add-{root.Id:D}']").Click();cut.Find("[data-testid='child-title']").Input("Keep this child");cut.Find("[data-testid='child-create']").Closest("form")!.Submit();cut.Find("[data-testid='todo-confirm']").Click();cut.WaitForAssertion(()=>Assert.NotNull(cut.Find("[data-testid='child-created']")));var child=f.Controller.LastReceipt!.TaskId;
        f.Client.State=f.Client.State with {Document=f.Client.State.Document with {Revision=12,Tasks=f.Client.State.Document.Tasks.Select(task=>task.Id==root.Id?task with {IsArchived=true}:task).ToArray()},StoreVersion=12};await todo.LoadSelectedAsync();cut.Find("[data-testid='child-attach']").Click();Assert.Null(f.Controller.Preview);Assert.Contains("remains independent",cut.Markup);Assert.Empty(cut.FindAll("[data-testid='child-create']"));Assert.Single(f.Client.AcceptedIds);Assert.Null(f.Controller.Snapshot!.Document.Tasks.Single(task=>task.Id==child).ParentTaskId);
        Button(cut,"Keep as independent Todo");Assert.Empty(cut.FindAll("[data-testid='child-created']"));Assert.Equal(2,f.Controller.Snapshot.Document.Tasks.Count);
    }

    [Fact]
    public async Task Exact_moves_reject_cycles_recover_original_receipts_and_detach_without_project_or_descendant_cascade()
    {
        var project=new WorkspaceProject(Guid.NewGuid(),1,"Project");var root=Task("Same name");var child=Task("Child",root.Id) with {ProjectId=project.Id};var target=Task("Same name");var(f,_)=await Setup(Doc(root,child,target) with {Projects=[project]});var cut=Render<TodoHierarchyPanel>();
        cut.Find($"[data-testid='tree-move-{root.Id:D}']").Click();cut.Find("[data-testid='move-parent']").Change(child.Id.ToString());cut.Find("[data-testid='move-review']").Closest("form")!.Submit();Assert.Null(f.Controller.Preview);Assert.Contains("cycle",cut.Markup);Assert.Empty(f.Client.AcceptedIds);
        cut.Find("[data-testid='move-parent']").Change(target.Id.ToString());cut.Find("[data-testid='move-review']").Closest("form")!.Submit();var command=f.Controller.Preview!.PreparedCommandId;Assert.Equal(target.Id,f.Controller.Preview.Command.Patch!.Relationships!.ParentTaskId);
        Save("todo-tree-move-review",cut.Markup,"same-name-exact-move-review",nameof(Exact_moves_reject_cycles_recover_original_receipts_and_detach_without_project_or_descendant_cascade));
        f.Client.LoseAcceptance=true;cut.Find("[data-testid='todo-confirm']").Click();cut.Find("[data-testid='todo-recover']").Click();Assert.Equal(command,f.Controller.LastReceipt!.CommandId);Assert.Equal(child,f.Controller.Snapshot!.Document.Tasks.Single(task=>task.Id==child.Id));
        cut.Find($"[data-testid='tree-expand-{target.Id:D}']").Click();cut.Find($"[data-testid='tree-detach-{root.Id:D}']").Click();Assert.True(f.Controller.Preview!.Command.Patch!.Relationships!.ClearParentTask);cut.Find("[data-testid='todo-confirm']").Click();Assert.Null(f.Controller.Snapshot!.Document.Tasks.Single(task=>task.Id==root.Id).ParentTaskId);Assert.Equal(child,f.Controller.Snapshot.Document.Tasks.Single(task=>task.Id==child.Id));Assert.Equal(2,f.Client.AcceptedIds.Count);
    }
    [Fact]
    public async Task Descendant_edit_uses_existing_fields_and_preserves_parent_and_independent_dates()
    {
        var root=Task("Parent");var child=Task("Editable child",root.Id) with {Notes="Old note",Deadline=new(2026,11,2)};var(f,_)=await Setup(Doc(root,child));var cut=Render<TodoHierarchyPanel>(p=>p.Add(x=>x.InitialTaskId,root.Id));
        cut.Find($"[data-testid='tree-edit-{child.Id:D}']").Click();cut.WaitForAssertion(()=>Assert.Equal("Editable child",cut.Find("[data-testid='todo-title']").GetAttribute("value")));
        cut.Find("[data-testid='todo-title']").Input("Edited child");cut.Find("[data-testid='todo-notes']").Input("Changed note");cut.Find("[data-testid='todo-title']").Closest("form")!.Submit();cut.Find("[data-testid='todo-confirm']").Click();
        var saved=f.Controller.Snapshot!.Document.Tasks.Single(task=>task.Id==child.Id);Assert.Equal(root.Id,saved.ParentTaskId);Assert.Equal(child.Deadline,saved.Deadline);Assert.Equal("Changed note",saved.Notes);Assert.Equal(root,f.Controller.Snapshot.Document.Tasks.Single(task=>task.Id==root.Id));
        Save("todo-tree-edit",cut.Markup,"descendant-edit-independent-fields",nameof(Descendant_edit_uses_existing_fields_and_preserves_parent_and_independent_dates));
    }
    [Fact]
    public async Task Project_lists_keep_other_project_branch_context_and_bounded_parent_choices()
    {
        var a=new WorkspaceProject(Guid.NewGuid(),1,"Project A",Alias:new string('a',64));var b=new WorkspaceProject(Guid.NewGuid(),1,"Project B");var root=Task("A root") with {ProjectId=a.Id};var child=Task("B child",root.Id) with {ProjectId=b.Id};var extras=Enumerable.Range(0,25).Select(i=>Task("Parent choice "+i)).ToArray();var(f,_)=await Setup(Doc([root,child,..extras]) with {Projects=[a,b]});var cut=Render<TodoHierarchyPanel>(p=>p.Add(x=>x.ProjectMode,true));
        Assert.Contains("Choose a project",cut.Markup);cut.Find("[data-testid='plan-project']").Change(a.Id.ToString());Assert.Single(cut.FindAll("[data-testid^='tree-row-']"));
        cut.Find($"[data-testid='tree-expand-{root.Id:D}']").Click();Assert.Contains("B child",cut.Markup);Assert.Contains("Project B",cut.Markup);Assert.Contains("Alias: "+a.Alias,cut.Markup);Save("todo-tree-project",cut.Markup,"cross-project-descendant-context",nameof(Project_lists_keep_other_project_branch_context_and_bounded_parent_choices),"/todo/projects");
        cut.Find($"[data-testid='tree-move-{child.Id:D}']").Click();cut.Find("[data-testid='move-review']").Closest("form")!.Submit();Assert.Null(f.Controller.Preview);Assert.Empty(f.Client.PreparationIds);Assert.True(cut.FindAll("[data-testid='move-parent'] option").Count<=21);Button(cut,"Next parents");Assert.Contains(extras[^1].Id.ToString(),cut.Find("[data-testid='move-parent']").InnerHtml);cut.Find("[data-testid='move-parent']").Change(extras[^1].Id.ToString());Button(cut,"Previous parents");Assert.Contains("retained selection",cut.Markup);Assert.Empty(f.Client.AcceptedIds);
    }
    [Fact]
    public async Task Stale_move_draft_requires_reload_and_account_change_clears_private_context()
    {
        var root=Task("Private parent");var target=Task("Private target");var(f,todo)=await Setup(Doc(root,target));var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-move-{root.Id:D}']").Click();cut.Find("[data-testid='move-parent']").Change(target.Id.ToString());
        f.Client.State=f.Client.State with {Document=f.Client.State.Document with {Revision=11},StoreVersion=11};await todo.LoadSelectedAsync();cut.Find("[data-testid='move-review']").Closest("form")!.Submit();Assert.Null(f.Controller.Preview);Assert.Contains("reload stale",cut.Markup);
        Button(cut,"Reload current relationship context");cut.Find("[data-testid='move-review']").Closest("form")!.Submit();Assert.Equal(11,f.Controller.Preview!.Command.ExpectedRevision);
        f.Session.SetState(f.Session.State with {IsAuthenticated=false,UserId=null});cut.WaitForAssertion(()=>Assert.DoesNotContain("Private",cut.Markup));Assert.DoesNotContain(root.Id.ToString(),cut.Markup);Assert.Empty(f.Client.AcceptedIds);Assert.Null(f.Controller.OriginalRequestId);
    }
    [Fact]
    public async Task Unknown_deep_link_recovers_to_roots_without_retaining_an_unavailable_error()
    {
        var root=Task("Available root");var(f,_)=await Setup(Doc(root));var cut=Render<TodoHierarchyPanel>(p=>p.Add(x=>x.InitialTaskId,Guid.NewGuid()));Assert.Contains("branch is unavailable",cut.Markup);Assert.Empty(cut.FindAll("[data-testid^='tree-row-']"));
        Save("todo-tree-unavailable",cut.Markup,"unavailable-exact-task-context",nameof(Unknown_deep_link_recovers_to_roots_without_retaining_an_unavailable_error));
        cut.Find("[data-testid='tree-roots']").Click();Assert.Single(cut.FindAll("[data-testid^='tree-row-']"));Assert.DoesNotContain("branch is unavailable",cut.Markup);Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Deep_linked_branch_renders_level_two_thousand_without_recursive_markup_or_increasing_indent()
    {
        var root=Task(new string('W',160)+" Root");var tasks=new List<CompanionTask>{root};for(var i=1;i<=2000;i++)tasks.Add(Task("Level "+i,tasks[^1].Id));var(f,_)=await Setup(Doc(tasks.ToArray()));var cut=Render<TodoHierarchyPanel>(p=>p.Add(x=>x.InitialTaskId,tasks[^2].Id));
        Assert.Single(cut.FindAll("[data-testid^='tree-row-']"));Assert.Contains("level 2000",cut.Markup);Assert.DoesNotContain("margin-left",cut.Markup);Save("todo-tree-deep",cut.Markup,"level-2000-focused-branch",nameof(Deep_linked_branch_renders_level_two_thousand_without_recursive_markup_or_increasing_indent));Assert.Empty(f.Client.AcceptedIds);
        cut.Find("[data-testid='tree-roots']").Click();cut.Find($"[data-testid='tree-expand-{root.Id:D}']").Click();cut.Find("[data-testid='tree-subtree']").Click();Assert.Equal(20,cut.FindAll("[data-testid^='tree-row-']").Count);cut.Find("[data-testid='tree-next']").Click();Assert.Equal(20,cut.FindAll("[data-testid^='tree-row-']").Count);Assert.Contains("earlier branch page",cut.Markup);
    }
}
