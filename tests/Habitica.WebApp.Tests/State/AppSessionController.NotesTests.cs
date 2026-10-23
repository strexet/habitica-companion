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
}
