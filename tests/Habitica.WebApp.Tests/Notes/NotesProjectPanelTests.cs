using Bunit;
using Habitica.Application.Notes;
using Habitica.Application.Workspace;
using Habitica.Domain.Notes;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Habitica.WebApp.Tests.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace Habitica.WebApp.Tests.Notes;

public sealed class NotesProjectPanelTests:BunitContext
{
    private static readonly Guid A=Guid.Parse("00000000-0000-4000-8000-000000000101"),B=Guid.Parse("00000000-0000-4000-8000-000000000102");
    private async Task<(NotesFixture Notes,WorkspaceTaskFixture Workspace,TodoWorkspacePresentation Todo)> Setup(bool cloud=false)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;
        var work=new WorkspaceTaskFixture();
        if(cloud){work.Client.Connected=true;await work.Controller.LoadAsync(true);}
        else await work.Controller.CreateLocalAsync();
        var state=work.Controller.Snapshot!;
        state=state with {Document=state.Document with {Projects=[new(A,1,"Identical project "+new string('W',180)),new(B,1,"Identical project "+new string('W',180))]}};
        if(cloud){work.Client.State=state;await work.Controller.LoadAsync(true);}
        else {work.Js.Rows[(WorkspaceTaskFixture.User,state.Document.Id.ToString("D"))]=WorkspaceJournalJson.Serialize(state);await work.Controller.LoadAsync(false,state.Document.Id);}
        var todo=new TodoWorkspacePresentation(work.Session,work.Client,work.Database,work.Controller);
        var f=new NotesFixture(todo,work.Database,work.Session);
        if(cloud){f.Client.Connected=true;await f.Controller.SwitchAsync(NotesSource.Cloud,true);f.Client.ProjectAuthority=new(f.Client.Book.OwnerScope,"cloud",state.Document);}
        await f.Controller.LoadAsync();
        Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton(f.Controller);Services.AddSingleton(todo);
        return(f,work,todo);
    }
    private static void Evidence(string name,string markup,string route,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,route,state,["form","actions","confirmation","disabled-controls","error"],test);
    private static async Task Save(NotesController c,string title="Private note",string markdown="# Exact\r\n```\r\ncontent 🧥\r\n```\r\n")
    {c.Draft.Title=title;c.Draft.Markdown=markdown;await c.SaveAsync();if(c.ProjectReview is not null)await c.AcceptProjectAsync();}

    [Fact] public async Task Create_attach_move_and_detach_review_exact_ids_and_keep_text_history()
    {
        var (f,work,todo)=await Setup();var c=f.Controller;var cut=Render<NotesPanel>();
        cut.Find("[data-testid=note-title]").Input("Project note");cut.Find("[data-testid=note-body]").Input("Exact\r\nMarkdown 🧥\r\n");cut.Find("[data-testid=note-project-picker]").Change(A.ToString());cut.Find("form").Submit();
        Assert.Null(f.Database.Current.Book);Assert.Contains(A.ToString(),cut.Find("[data-testid=note-project-review]").TextContent);
        Evidence("notes-project-create-review",cut.Markup,"/notes","new-exact-project-review",nameof(Create_attach_move_and_detach_review_exact_ids_and_keep_text_history));
        cut.Find("[data-testid=note-project-confirm]").Click();Assert.False(c.HasPending);Assert.Equal(A,c.Selected!.Current.Project!.ProjectId);var id=c.SelectedId;var body=c.Draft.Markdown;
        cut.Find("[data-testid=note-project-picker]").Change(B.ToString());cut.Find("form").Submit();Assert.Contains(A.ToString(),cut.Markup);Assert.Contains(B.ToString(),cut.Find("[data-testid=note-project-review]").TextContent);cut.Find("[data-testid=note-project-confirm]").Click();
        Assert.Equal(id,c.SelectedId);Assert.Equal(body,c.Selected!.Current.Markdown);Assert.Equal(A,c.Selected.History.Single().Project!.ProjectId);
        cut.Find("[data-testid=note-unassign]").Click();cut.Find("form").Submit();cut.Find("[data-testid=note-project-confirm]").Click();Assert.Null(c.Selected!.Current.Project);Assert.Equal(body,c.Draft.Markdown);Assert.Equal(3,c.Selected.Current.Revision);Assert.Equal(2,f.Database.Current.ProjectCommits);Assert.Empty(work.Controller.Snapshot!.Commands);
        Assert.True(c.UseVersion(1));Assert.Equal(A,c.Draft.Project!.ProjectId);Assert.True(c.Dirty);Assert.Equal(3,c.Selected.Current.Revision);
    }

    [Fact] public async Task Combined_project_text_trash_and_unassigned_filters_page_beyond_twenty_without_losing_drafts()
    {
        var (f,_,_)=await Setup();var c=f.Controller;
        for(var i=0;i<27;i++){c.NewNote(true);c.ChooseProject(i<24?A:null);await Save(c,"Shared note "+i);}
        c.NewNote(true);var cut=Render<NotesPanel>();cut.Find("[data-testid=notes-project-filter]").Change(A.ToString());Assert.Equal(20,cut.FindAll("[data-testid=note-open]").Count);cut.Find("[data-testid=notes-next]").Click();Assert.Equal(4,cut.FindAll("[data-testid=note-open]").Count);Assert.True(cut.Find("[data-testid=notes-next]").HasAttribute("disabled"));
        cut.Find("[data-testid=notes-project-filter]").Change("unassigned");Assert.Equal(3,cut.FindAll("[data-testid=note-open]").Count);cut.Find("[data-testid=notes-search]").Input("26");Assert.Single(cut.FindAll("[data-testid=note-open]"));cut.Find("[data-testid=note-open]").Click();c.Draft.Markdown="Private draft";Assert.True(c.ChooseProject(A));
        cut.Find("[data-testid=note-new]").Click();Assert.Single(cut.FindAll("[aria-label='Unsaved draft']"));Assert.Equal("Private draft",c.Draft.Markdown);cut.Find("[aria-label='Unsaved draft'] button:nth-child(2)").Click();Assert.Equal(A,c.Draft.Project!.ProjectId);
        c.NewNote(true);c.Open(c.Book!.Notes.Last().Id);Assert.True(c.PrepareLifecycle(NoteOperation.Trash));await c.AcceptLifecycleAsync();cut.Find("[data-testid=notes-trash-filter]").Change(true);Assert.Single(cut.FindAll("[data-testid=note-open]"));
        Evidence("notes-project-filter",cut.Markup,"/notes","combined-unassigned-text-trash",nameof(Combined_project_text_trash_and_unassigned_filters_page_beyond_twenty_without_losing_drafts));
    }

    [Fact] public async Task Stale_project_states_preserve_content_and_allow_reviewed_detach_without_project_authority()
    {
        var (f,work,todo)=await Setup();var c=f.Controller;c.ChooseProject(A);await Save(c);var original=c.Selected!.Current.Markdown;
        var cut=Render<NotesPanel>();var baseline=work.Controller.Snapshot!;
        foreach(var status in new[]{"archived","removed","missing"})
        {
            var state=baseline with {StoreVersion=baseline.StoreVersion+1,Document=baseline.Document with {Projects=status=="missing"?[]:[new(A,1,"Stale project",IsArchived:status=="archived")],Revision=baseline.Document.Revision+1}};
            if(status=="removed")state=WorkspaceOrganizationEngine.Accept(baseline,new(Guid.NewGuid(),baseline.Document.Id,baseline.Document.Revision,WorkspaceOrganizationKind.Project,WorkspaceOrganizationOperation.Remove,A,new(Removal:WorkspaceProjectRemoval.From(baseline.Document,A))),DateTimeOffset.UtcNow).Snapshot!;
            work.Js.Rows[(WorkspaceTaskFixture.User,state.Document.Id.ToString("D"))]=WorkspaceJournalJson.Serialize(state);await cut.InvokeAsync(()=>todo.LoadSelectedAsync());Assert.Contains(status,cut.Find("[data-testid=note-project-current]").TextContent);Assert.Equal(original,c.Draft.Markdown);
        }
        c.Draft.Markdown=original+"preserved";await c.SaveAsync();Assert.Null(c.ProjectReview);Assert.Equal(A,c.Selected!.Current.Project!.ProjectId);
        cut.Find("[data-testid=note-unassign]").Click();cut.Find("form").Submit();Evidence("notes-project-stale",cut.Markup,"/notes","missing-link-detach-review",nameof(Stale_project_states_preserve_content_and_allow_reviewed_detach_without_project_authority));cut.Find("[data-testid=note-project-confirm]").Click();Assert.Null(c.Selected!.Current.Project);
    }

    [Fact] public async Task Original_link_receipt_survives_acknowledgement_loss_and_later_project_changes()
    {
        var (f,_,_)=await Setup();var c=f.Controller;await Save(c);c.ChooseProject(A);await c.SaveAsync();f.Database.Current.LoseResponse=true;await c.AcceptProjectAsync();Assert.True(c.HasPending);var original=c.PendingCommandId;Assert.False(c.NewNote(true));Assert.False(await c.SwitchAsync(NotesSource.Cloud,true));
        var cut=Render<NotesPanel>();Evidence("notes-project-unknown",cut.Markup,"/notes","unknown-original-association",nameof(Original_link_receipt_survives_acknowledgement_loss_and_later_project_changes));await cut.InvokeAsync(()=>c.RecoverAsync());Assert.False(c.HasPending);Assert.Equal(original,c.LastReceipt!.CommandId);Assert.Equal(2,c.Book!.Revision);Assert.Equal(1,f.Database.Current.ProjectCommits);
    }

    [Fact] public async Task Project_route_opens_and_creates_in_exact_context_and_preserves_draft_on_hiding()
    {
        var (f,_,_)=await Setup();var c=f.Controller;var cut=Render<TodoProjectNotesPanel>(p=>p.Add(x=>x.ProjectId,A));Assert.Empty(cut.FindAll("[data-testid=note-title]"));cut.Find("[data-testid=project-notes-toggle]").Click();cut.Find("[data-testid=note-new]").Click();Assert.Equal(A,c.Draft.Project!.ProjectId);cut.Find("[data-testid=note-title]").Input("Long project document "+new string('T',200));cut.Find("[data-testid=note-body]").Input(new string('W',500));cut.Find("form").Submit();
        Evidence("project-notes-review",cut.Markup,"/todo/projects","exact-project-create-review",nameof(Project_route_opens_and_creates_in_exact_context_and_preserves_draft_on_hiding));cut.Find("[data-testid=note-project-confirm]").Click();Assert.Single(cut.FindAll("[data-testid=note-open]"));
        cut.Find("[data-testid=note-body]").Input("Private draft kept across project navigation");cut.Find("[data-testid=project-notes-toggle]").Click();Assert.True(c.Dirty);cut.Find("[data-testid=project-notes-toggle]").Click();Assert.Equal("Private draft kept across project navigation",c.Draft.Markdown);
        Evidence("project-notes-draft",cut.Markup,"/todo/projects","project-private-draft",nameof(Project_route_opens_and_creates_in_exact_context_and_preserves_draft_on_hiding));
    }

    [Fact] public async Task Project_only_cloud_access_never_renders_private_browser_notes_or_connects_notes()
    {
        var (f,_,_)=await Setup(cloud:true);var c=f.Controller;await c.SwitchAsync(NotesSource.Browser,true);await Save(c,"Private browser title","Private browser snippet");var cut=Render<TodoProjectNotesPanel>(p=>p.Add(x=>x.ProjectId,A));cut.Find("[data-testid=project-notes-toggle]").Click();Assert.DoesNotContain("Private browser title",cut.Markup);Assert.DoesNotContain("Private browser snippet",cut.Markup);Assert.Contains("Choose that source explicitly",cut.Markup);
        f.Client.Connected=false;cut.Find("[data-testid=note-source]").Change("Cloud");Assert.Empty(cut.FindAll("[data-testid=note-body]"));Assert.Single(cut.FindAll("[data-testid=notes-consent]"));Assert.Equal(0,f.Client.Submits);
        Evidence("project-notes-no-consent",cut.Markup,"/todo/projects","separate-cloud-consent-no-content",nameof(Project_only_cloud_access_never_renders_private_browser_notes_or_connects_notes));
    }

    [Fact] public async Task Review_refuses_changed_project_or_content_and_account_change_clears_private_link_draft()
    {
        var (f,_,_)=await Setup();var c=f.Controller;await Save(c);c.ChooseProject(A);await c.SaveAsync();c.Draft.Markdown="Changed after review";await c.AcceptProjectAsync();Assert.Null(c.ProjectReview);Assert.Equal(1,c.Book!.Revision);Assert.Equal("Changed after review",c.Draft.Markdown);
        await c.SaveAsync();Assert.NotNull(c.ProjectReview);var cut=Render<NotesPanel>();f.Session.SetState(f.Session.State with {UserId=Guid.NewGuid().ToString("D")});cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid=note-project-review]")));Assert.Null(c.Draft.Project);Assert.Null(c.Book);Assert.DoesNotContain("Changed after review",cut.Markup);
    }

    [Fact] public async Task Cloud_attach_move_detach_and_read_failure_keep_ownership_and_draft_explicit()
    {
        var (f,_,_)=await Setup(cloud:true);var c=f.Controller;c.ChooseProject(A);await Save(c);Assert.Equal("cloud",c.Selected!.Current.Project!.Source);var id=c.SelectedId;c.ChooseProject(B);await c.SaveAsync();f.Client.LoseResponse=true;await c.AcceptProjectAsync();Assert.True(c.HasPending);await c.RecoverAsync();Assert.Equal(B,c.Selected!.Current.Project!.ProjectId);Assert.Equal(2,f.Client.Submits);Assert.Equal(id,c.SelectedId);
        c.ChooseProject(null);await c.SaveAsync();await c.AcceptProjectAsync();Assert.Null(c.Selected!.Current.Project);c.Draft.Markdown="Private cloud draft";f.Client.FailLoads=true;await c.LoadAsync();Assert.Null(c.Book);Assert.Equal("Private cloud draft",c.Draft.Markdown);Assert.True(c.Dirty);Assert.Null(c.Search("",false,0,null));
    }

    [Fact] public async Task Retained_filter_never_broadens_when_workspace_changes_and_stale_review_preserves_draft()
    {
        var (f,work,todo)=await Setup();var c=f.Controller;c.ChooseProject(A);await Save(c,"Linked note");c.NewNote(true);await Save(c,"Unassigned private note");
        var cut=Render<NotesPanel>();cut.Find("[data-testid=notes-project-filter]").Change(A.ToString());Assert.Single(cut.FindAll("[data-testid=note-open]"));
        await cut.InvokeAsync(()=>todo.CreateBrowserAsync());Assert.Single(cut.FindAll("[data-testid=note-open]"));Assert.Contains("Linked note",cut.Find("[data-testid=note-open]").TextContent);Assert.DoesNotContain("Unassigned private note",cut.Find(".notes-library").TextContent);
        var selected=todo.Selection!;Assert.NotEqual(work.Js.Rows.Keys.First().Id,selected.WorkspaceId.ToString("D"));Assert.Empty(c.ProjectChoices);
        cut.Find("[data-testid=note-open]").Click();Assert.Contains("unavailable",cut.Find("[data-testid=note-project-current]").TextContent);cut.Find("[data-testid=note-unassign]").Click();cut.Find("form").Submit();
        var current=f.Database.Current.Book!;f.Database.Current.Book=NotesEngine.Apply(current,new(Guid.NewGuid(),Guid.NewGuid(),current.Revision,0,NoteOperation.Create,"Concurrent note","Other content"),DateTimeOffset.UtcNow).Notebook;
        await cut.InvokeAsync(()=>c.LoadAsync());cut.Find("[data-testid=note-project-confirm]").Click();Assert.Null(c.ProjectReview);Assert.True(c.Dirty);Assert.Null(c.Draft.Project);Assert.Equal(A,c.Selected!.Current.Project!.ProjectId);Assert.Contains("changed",c.Message!);
    }

    [Fact] public async Task Archived_project_context_disables_new_links_and_retains_exact_existing_notes()
    {
        var (f,work,todo)=await Setup();var c=f.Controller;c.ChooseProject(A);await Save(c);var id=c.SelectedId;
        var state=work.Controller.Snapshot!;state=WorkspaceOrganizationEngine.Accept(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOrganizationKind.Project,WorkspaceOrganizationOperation.Archive,A),DateTimeOffset.UtcNow).Snapshot!;
        work.Js.Rows[(WorkspaceTaskFixture.User,state.Document.Id.ToString("D"))]=WorkspaceJournalJson.Serialize(state);await todo.LoadSelectedAsync();
        var cut=Render<TodoProjectNotesPanel>(p=>p.Add(x=>x.ProjectId,A));cut.Find("[data-testid=project-notes-toggle]").Click();Assert.True(cut.Find("[data-testid=note-new]").HasAttribute("disabled"));Assert.Contains("unavailable for new links",cut.Markup);Assert.Single(cut.FindAll("[data-testid=note-open]"));Assert.Equal(id,c.SelectedId);
        Evidence("project-notes-archived",cut.Markup,"/todo/projects","archived-context-disabled-create-retained-note",nameof(Archived_project_context_disables_new_links_and_retains_exact_existing_notes));
    }
}
