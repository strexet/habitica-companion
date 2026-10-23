using Habitica.Application.Notes;
using Habitica.Domain.Notes;
using Habitica.WebApp.State;
namespace Habitica.WebApp.Tests.Notes;
public sealed class NotesControllerTests
{
    private static async Task Save(NotesController c,string title,string text){c.NewNote(true);c.Draft.Title=title;c.Draft.Markdown=text;await c.SaveAsync();Assert.NotNull(c.LastReceipt);}
    [Fact]public async Task Full_markdown_roundtrip_keeps_exact_source_and_prior_versions_and_explicit_source_boundaries()
    {
        var f=new NotesFixture();var c=f.Controller;await c.LoadAsync();var source="# Размеры 🧥\r\n| Item | Size |\r\n| --- | --- |\r\n| coat | L |\r\n```html\r\n<script>x</script>\r\n```\r\n\\tail\n";
        await Save(c,"Одежда",source);Assert.Equal(source,Assert.Single(f.Database.Current.Book!.Notes).Current.Markdown);
        c.Draft.Markdown+="\nnew fact";await c.SaveAsync();Assert.Equal(source,Assert.Single(c.Selected!.History).Markdown);
        Assert.True(c.UseVersion(1));Assert.True(c.Dirty);Assert.False(await c.SwitchAsync(NotesSource.Cloud));Assert.Equal(source,c.Draft.Markdown);
        Assert.True(await c.SwitchAsync(NotesSource.Cloud,true));Assert.Null(c.Book);Assert.Null(c.SelectedId);await c.ConnectAsync(false);Assert.False(f.Client.Connected);await c.ConnectAsync(true);Assert.Empty(c.Book!.Notes);Assert.Equal(0,f.Client.Submits);Assert.Single(f.Database.Current.Book.Notes);
    }
    [Fact]public async Task External_change_preserves_draft_and_blocks_overwrite_until_explicit_editor_reload()
    {
        var f=new NotesFixture();var c=f.Controller;await c.LoadAsync();await Save(c,"Original","first");var id=c.SelectedId!.Value;c.Draft.Markdown="my draft";
        var book=f.Database.Current.Book!;f.Database.Current.Book=NotesEngine.Apply(book,new(Guid.NewGuid(),id,book.Revision,1,NoteOperation.Update,Markdown:"other device"),DateTimeOffset.UtcNow).Notebook;
        await c.LoadAsync();Assert.True(c.HasConflict);Assert.Equal("my draft",c.Draft.Markdown);await c.SaveAsync();Assert.Contains("stored note changed",c.Message);Assert.Equal(2,f.Database.Current.Book.Revision);
        Assert.False(c.Open(id));Assert.True(c.Open(id,true));Assert.Equal("other device",c.Draft.Markdown);Assert.False(c.HasConflict);
    }
    [Fact]public async Task Lost_acceptance_recovers_original_receipt_once_without_duplicate_writes()
    {
        var f=new NotesFixture();var c=f.Controller;await c.SwitchAsync(NotesSource.Cloud);await c.ConnectAsync(true);f.Client.LoseResponse=true;c.Draft.Title="Travel";c.Draft.Markdown="full document";await c.SaveAsync();
        Assert.True(c.HasPending);Assert.False(c.NewNote(true));Assert.False(await c.SwitchAsync(NotesSource.Browser,true));await c.RecoverAsync();Assert.False(c.HasPending);Assert.Equal("full document",c.Selected!.Current.Markdown);Assert.Equal(1,f.Client.Submits);Assert.Equal(1,f.Client.Recoveries);Assert.Single(c.Book!.Receipts);
    }
    [Fact]public async Task Browser_lost_acknowledgement_recovers_without_saving_again()
    {
        var f=new NotesFixture();var c=f.Controller;await c.LoadAsync();f.Database.Current.LoseResponse=true;c.Draft.Title="draft";c.Draft.Markdown="saved";await c.SaveAsync();Assert.True(c.HasPending);await c.RecoverAsync();Assert.False(c.HasPending);Assert.Equal(1,c.Book!.Revision);Assert.Single(c.Book.Receipts);
    }
    [Fact]public async Task Trash_restore_and_purge_require_exact_reviews_and_remove_retained_bodies()
    {
        var f=new NotesFixture();var c=f.Controller;await c.LoadAsync();await Save(c,"Secret","private");c.Draft.Markdown="revised";await c.SaveAsync();Assert.True(c.PrepareLifecycle(NoteOperation.Trash));c.CancelReview();Assert.False(c.Selected!.Current.IsTrashed);Assert.True(c.PrepareLifecycle(NoteOperation.Trash));await c.AcceptLifecycleAsync();Assert.True(c.Selected!.Current.IsTrashed);
        Assert.True(c.PrepareLifecycle(NoteOperation.Restore));await c.AcceptLifecycleAsync();Assert.False(c.Selected!.Current.IsTrashed);Assert.True(c.PrepareLifecycle(NoteOperation.Trash));await c.AcceptLifecycleAsync();Assert.True(c.PrepareLifecycle(NoteOperation.Purge));Assert.Equal(5,c.Review!.Versions);await c.AcceptLifecycleAsync();Assert.Empty(c.Book!.Notes);Assert.Single(c.Book.Purged);Assert.Empty(c.Draft.Markdown!);Assert.DoesNotContain("private",NotesJson.Serialize(f.Database.Current.Book!));
    }
    [Fact]public async Task Account_switch_and_late_read_clear_private_data_and_cannot_restore_old_notebook()
    {
        var f=new NotesFixture();var c=f.Controller;await c.LoadAsync();await Save(c,"Old account","private");var store=f.Database.Current;store.Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);store.Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=c.LoadAsync();await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));f.Session.SetState(f.Session.State with{UserId=Guid.NewGuid().ToString("D")});store.Release.SetResult();await pending;Assert.Null(c.Book);Assert.Empty(c.Draft.Markdown!);await c.LoadAsync();Assert.Empty(c.Book!.Notes);Assert.Equal(2,f.Database.Stores.Count);
    }
    [Fact]public async Task Import_limits_unavailable_reads_and_cleanup_preserve_distinct_states()
    {
        var f=new NotesFixture();var c=f.Controller;await c.LoadAsync();Assert.True(c.ImportMarkdown("Document.md","# Exact\r\ntext"));Assert.Equal("Document",c.Draft.Title);Assert.False(c.ImportMarkdown("Another.md","replace"));Assert.False(c.ImportMarkdown("big.md",new string('x',NotesModel.BodyBytes+1),true));Assert.Equal("# Exact\r\ntext",c.Draft.Markdown);
        f.Database.Current.Unavailable=true;await c.LoadAsync();Assert.True(c.IsError);Assert.True(c.Dirty);await c.SwitchAsync(NotesSource.Cloud,true);await c.ConnectAsync(true);await Save(c,"Cloud","cloud body");await c.DisconnectCloudAsync(false);Assert.False(c.CloudConnected);Assert.Null(c.Book);Assert.Single(f.Client.Book.Notes);Assert.Empty(c.Draft.Markdown!);
    }
}
