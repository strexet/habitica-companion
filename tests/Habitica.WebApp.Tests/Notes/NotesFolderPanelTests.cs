using Bunit;
using Habitica.Application.Notes;
using Habitica.Domain.Notes;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
namespace Habitica.WebApp.Tests.Notes;

public sealed class NotesFolderPanelTests:BunitContext
{
    private NotesFixture Setup(){JSInterop.Mode=JSRuntimeMode.Loose;var f=new NotesFixture();Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton(f.Controller);return f;}
    private static void Evidence(string name,string markup,string state)=>Qualification.PageLayoutEvidence.Save(name,markup,"/notes",state,["form","actions","confirmation","disabled-controls","error"],nameof(NotesFolderPanelTests));
    private static Guid Create(IRenderedComponent<NotesPanel> cut,NotesFixture f,string name)
    {
        cut.Find("[data-testid=folder-name]").Input(name);cut.Find("[data-testid=folder-create]").Click();Assert.NotNull(f.Controller.FolderReview);var command=f.Controller.FolderReview.Command;Assert.DoesNotContain(f.Controller.Folders,folder=>folder.Id==command.FolderId);cut.Find("[data-testid=folder-confirm]").Click();cut.WaitForAssertion(()=>Assert.Equal(command.Id,f.Controller.LastFolderReceipt?.CommandId));return f.Controller.LastFolderReceipt!.FolderId;
    }
    [Fact]
    public void Standalone_folder_capture_move_and_detach_preserve_exact_Markdown_and_original_versions()
    {
        var f=Setup();var cut=Render<NotesPanel>();var a=Create(cut,f,"Hotels 🧥");Assert.Empty(f.Controller.Book!.Notes);cut.Find("[data-testid=note-new]").Click();Assert.Equal(a,f.Controller.DraftFolder!.FolderId);
        const string body="# Hotel\r\nHost: full details\nFood: exact\n```md\n<>&\\\n```";cut.Find("[data-testid=note-title]").Input("One hotel");cut.Find("[data-testid=note-body]").Input(body);cut.Find("form").Submit();Assert.NotNull(f.Controller.ProjectReview);Assert.Null(f.Database.Current.Book!.Notes.FirstOrDefault());cut.Find("[data-testid=note-project-confirm]").Click();Assert.Equal(body,f.Controller.Selected!.Current.Markdown);var id=f.Controller.SelectedId;
        var b=Create(cut,f,"Hotels 🧥");Assert.NotEqual(a,b);Assert.Equal(2,f.Controller.Folders.Count);cut.Find("[data-testid=note-folder-picker]").Change(b.ToString());cut.Find("form").Submit();Assert.Equal(a,f.Controller.ProjectReview!.FolderBefore!.FolderId);Assert.Equal(b,f.Controller.ProjectReview.FolderAfter!.FolderId);cut.Find("[data-testid=note-project-confirm]").Click();Assert.Equal(id,f.Controller.SelectedId);Assert.Equal(body,f.Controller.Selected!.Current.Markdown);Assert.Equal(a,f.Controller.Selected.History[0].Folder!.FolderId);
        cut.Find("[data-testid=note-clear-folder]").Click();cut.Find("form").Submit();cut.Find("[data-testid=note-project-confirm]").Click();Assert.Null(f.Controller.Selected!.Current.Folder);Assert.Equal(body,f.Controller.Selected.Current.Markdown);cut.Find("[data-testid=notes-folder-filter]").Change("none");Assert.Single(cut.FindAll("[data-testid=note-open]"));Evidence("notes-folders-document",cut.Markup,"duplicate-folders-exact-content-detach");Assert.True(f.Controller.UseVersion(1));Assert.Equal(a,f.Controller.DraftFolder!.FolderId);Assert.Equal(body,f.Controller.Draft.Markdown);
    }
    [Fact]
    public void Nonempty_folder_removal_includes_trash_and_never_deletes_a_note()
    {
        var f=Setup();var cut=Render<NotesPanel>();var id=Create(cut,f,"Private folder");cut.Find("[data-testid=note-new]").Click();cut.Find("[data-testid=note-title]").Input("Member");cut.Find("[data-testid=note-body]").Input("Private body");cut.Find("form").Submit();cut.Find("[data-testid=note-project-confirm]").Click();cut.Find("[data-testid=note-trash]").Click();cut.Find("[data-testid=note-action-confirm]").Click();Assert.True(f.Controller.Selected!.Current.IsTrashed);
        Assert.True(cut.Find("[data-testid=folder-remove]").HasAttribute("disabled"));Assert.Contains("including trash",cut.Markup);Assert.False(f.Controller.PrepareFolder(NoteFolderOperation.Remove,id,null));Assert.Single(f.Controller.Book!.Notes);Assert.Equal("Private body",f.Controller.Selected.Current.Markdown);Evidence("notes-folders-trash",cut.Markup,"nonempty-including-trash-refusal");
    }
    [Fact]
    public void Folder_reviews_preserve_dirty_drafts_and_clear_private_context_on_account_changes()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-title]").Input("Unsaved private draft");cut.Find("[data-testid=note-body]").Input("Exact source");Assert.True(cut.Find("[data-testid=folder-create]").HasAttribute("disabled"));Assert.Equal("Exact source",f.Controller.Draft.Markdown);cut.Find("[data-testid=note-new]").Click();cut.Find("[data-testid=note-discard]").Click();cut.Find("[data-testid=folder-name]").Input("Private pending folder");cut.Find("[data-testid=folder-create]").Click();Assert.NotNull(f.Controller.FolderReview);Assert.True(cut.Find("[data-testid=note-new]").HasAttribute("disabled"));Evidence("notes-folders-review",cut.Markup,"explicit-empty-folder-create-review");
        f.Session.SetState(f.Session.State with{UserId=Guid.NewGuid().ToString("D")});cut.WaitForAssertion(()=>Assert.DoesNotContain("Private pending folder",cut.Markup));Assert.Null(f.Controller.FolderReview);Assert.Null(f.Controller.DraftFolder);Assert.Empty(f.Controller.Draft.Markdown!);
    }
    [Fact]
    public async Task Original_folder_acknowledgment_loss_recovers_once_without_an_extra_metadata_write()
    {
        var f=Setup();await f.Controller.SwitchAsync(NotesSource.Cloud);await f.Controller.ConnectAsync(true);var cut=Render<NotesPanel>();f.Client.LoseResponse=true;cut.Find("[data-testid=folder-name]").Input("Cloud folder");cut.Find("[data-testid=folder-create]").Click();cut.Find("[data-testid=folder-confirm]").Click();Assert.True(f.Controller.HasPending);Assert.False(f.Controller.CanWrite);Assert.Single(f.Client.Book.Folders!);Evidence("notes-folders-unknown",cut.Markup,"original-folder-receipt-recovery");cut.Find("[aria-label='Uncertain save'] button").Click();cut.WaitForAssertion(()=>Assert.False(f.Controller.HasPending));Assert.Equal(1,f.Client.FolderSubmits);Assert.Equal(1,f.Client.FolderRecoveries);Assert.NotNull(f.Controller.LastFolderReceipt);Assert.Single(f.Controller.Folders);
    }
    [Fact]
    public async Task A_retained_stale_project_folder_is_readable_and_explicit_detach_preserves_the_project()
    {
        var f=Setup();var c=f.Controller;await c.SwitchAsync(NotesSource.Cloud);await c.ConnectAsync(true);var scope=f.Client.Book.OwnerScope;var project=new NoteProjectLink(Guid.NewGuid(),Guid.NewGuid(),"cloud",scope);var authority=new NotesProjectAuthority(scope,"cloud",new Habitica.Domain.Workspace.WorkspaceDocument{Id=project.WorkspaceId,Projects=[new(project.ProjectId,1,"Trip")]});var folder=Guid.NewGuid();var first=NotesFolders.Apply(f.Client.Book,new(Guid.NewGuid(),folder,0,0,NoteFolderOperation.Create,scope,"cloud","Hotels",project),DateTimeOffset.UtcNow,authority);Assert.True(first.Accepted);
        var note=NotesEngine.Apply(first.Notebook,new(Guid.NewGuid(),Guid.NewGuid(),1,0,NoteOperation.Create,"Retained","Exact body",Project:project,Folder:new(folder,scope,"cloud")),DateTimeOffset.UtcNow,authority);Assert.True(note.Accepted);f.Client.Book=note.Notebook;await c.LoadAsync();c.Open(note.Notebook.Notes[0].Id);var cut=Render<NotesPanel>();Assert.Contains("Retained",cut.Markup);Assert.False(c.FolderSelectable(folder));cut.Find("[data-testid=note-clear-folder]").Click();cut.Find("form").Submit();cut.Find("[data-testid=note-project-confirm]").Click();Assert.Null(c.Selected!.Current.Folder);Assert.Equal(project,c.Selected.Current.Project);Assert.Equal("Exact body",c.Selected.Current.Markdown);Evidence("notes-folders-stale",cut.Markup,"retained-project-detach");
    }
    [Fact]
    public async Task Late_folder_acceptance_cannot_restore_private_metadata_after_account_change()
    {
        var f=Setup();var c=f.Controller;await c.SwitchAsync(NotesSource.Cloud);await c.ConnectAsync(true);var cut=Render<NotesPanel>();f.Client.FolderEntered=new(TaskCreationOptions.RunContinuationsAsynchronously);f.Client.FolderRelease=new(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(c.PrepareFolder(NoteFolderOperation.Create,null,"Old owner private name"));var pending=c.AcceptFolderAsync();await f.Client.FolderEntered.Task;Assert.True(c.HasPending);f.Session.SetState(f.Session.State with{UserId=Guid.NewGuid().ToString("D")});f.Client.FolderRelease.SetResult();await pending;
        Assert.Null(c.Book);Assert.Empty(c.Folders);Assert.Null(c.LastFolderReceipt);Assert.Null(c.FolderReview);Assert.False(c.HasPending);cut.WaitForAssertion(()=>Assert.DoesNotContain("Old owner private name",cut.Markup));Assert.Equal(1,f.Client.FolderSubmits);
    }
    [Fact]
    public async Task Uncertain_folder_recovery_refuses_a_replaced_cloud_notebook_without_replaying()
    {
        var f=Setup();var c=f.Controller;await c.SwitchAsync(NotesSource.Cloud);await c.ConnectAsync(true);f.Client.LoseResponse=true;Assert.True(c.PrepareFolder(NoteFolderOperation.Create,null,"Original notebook folder"));await c.AcceptFolderAsync();Assert.True(c.HasPending);var original=c.PendingCommandId;f.Client.Id=Guid.NewGuid();await c.RecoverAsync();Assert.True(c.HasPending);Assert.Equal(original,c.PendingCommandId);Assert.Equal(0,f.Client.FolderRecoveries);Assert.Equal(1,f.Client.FolderSubmits);Assert.Contains("another notebook",c.Message!);
    }

}
