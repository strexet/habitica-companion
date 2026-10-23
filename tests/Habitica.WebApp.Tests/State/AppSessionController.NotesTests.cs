using Habitica.Application.Notes;
using Habitica.Domain.Notes;
namespace Habitica.WebApp.Tests.State;
public sealed partial class AppSessionControllerTests
{
    [Fact]
    public async Task Account_switch_and_logout_clear_notes_drafts_without_exporting_them()
    {
        var drafts=new NotesDraftSession();var controller=CreateController(new FakeDiagnosticsLogStore([]),notesDrafts:drafts);
        await controller.SignInAsync(new(){UserId="synthetic-a",ApiToken="synthetic-secret",PersistLocally=false});
        drafts.Bind(NotesModel.Scope("synthetic-a"));drafts.Markdown="private draft";
        await controller.SignInAsync(new(){UserId="synthetic-b",ApiToken="synthetic-secret",PersistLocally=false});Assert.Null(drafts.Markdown);Assert.Null(drafts.OwnerScope);
        drafts.Bind(NotesModel.Scope("synthetic-b"));drafts.Markdown="other private draft";await controller.LogoutAsync();Assert.Null(drafts.Markdown);Assert.Null(drafts.OwnerScope);
    }
    [Fact]
    public async Task Notes_cloud_tab_credentials_are_physically_forgotten_on_account_switch_signout_clear_and_integration_disconnect()
    {
        var notes=new Habitica.WebApp.Tests.Notes.NotesFixture.Cloud();var controller=CreateController(new FakeDiagnosticsLogStore([]),notesCloud:notes,integrationClient:new IntegrationFixture());
        await controller.SignInAsync(new(){UserId="synthetic-a",ApiToken="synthetic-secret",PersistLocally=false});notes.Connected=true;var before=notes.Forgotten;
        await controller.SignInAsync(new(){UserId="synthetic-b",ApiToken="synthetic-secret",PersistLocally=false});Assert.False(notes.Connected);Assert.True(notes.Forgotten>before);
        notes.Connected=true;before=notes.Forgotten;await controller.DisconnectChatGptAsync(false);Assert.False(notes.Connected);Assert.True(notes.Forgotten>before);
        notes.Connected=true;before=notes.Forgotten;await controller.ClearLocalDataAsync();Assert.False(notes.Connected);Assert.True(notes.Forgotten>before);
        await controller.SignInAsync(new(){UserId="synthetic-b",ApiToken="synthetic-secret",PersistLocally=false});notes.Connected=true;before=notes.Forgotten;await controller.LogoutAsync();Assert.False(notes.Connected);Assert.True(notes.Forgotten>before);
    }
}
