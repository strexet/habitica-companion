using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.State;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class WorkspaceConversionControllerTests
{
    [Fact]
    public async Task Explicit_ten_item_review_preserves_duplicate_identities_notes_and_original_lost_ack_receipt()
    {
        var f=new WorkspaceTaskFixture();var api=new ChecklistConversionFixture {LosePreparation=true,LoseAcceptance=true};using var controller=new WorkspaceConversionController(f.Session,api);
        await controller.LoadAsync(api.Source.Id);Assert.All(controller.Drafts,row=>{Assert.Null(row.Mode);Assert.Null(row.DesiredState);Assert.Null(row.TargetTaskId);});
        await controller.PrepareAsync();Assert.Empty(api.Requests);Assert.Contains("explicit",controller.Message);
        api.ChooseReuse(controller);await controller.PrepareAsync();var request=Assert.Single(api.Requests);Assert.True(controller.HasPendingIntent);Assert.Null(controller.Preview);
        controller.CancelReview();Assert.NotNull(controller.Request);await controller.RetryPreparationAsync();Assert.Equal(request,api.Requests[1]);Assert.NotNull(controller.Preview);
        Assert.False(controller.CanConfirm);controller.AcknowledgePage(true);Assert.True(controller.CanConfirm);
        var before=api.State.Document.Completions;var originalSource=api.Source;await controller.ConfirmAsync();Assert.True(controller.AcceptanceUnknown);controller.CancelReview();Assert.NotNull(controller.Preview);
        await controller.RecoverAsync();Assert.False(controller.HasPendingIntent);Assert.Equal(originalSource,api.Source);Assert.Equal(before,api.State.Document.Completions);Assert.Equal(11,api.State.Document.Tasks.Count);Assert.Equal(2,api.State.Document.Tasks.Count(task=>task.IsArchived));
        Assert.Equal(10,controller.Status!.Targets.Count);Assert.False(controller.Status.StoredCorrespondenceComplete);Assert.False(controller.Status.RetirementReady);Assert.All(controller.Drafts,row=>Assert.True(row.Bound));Assert.All(controller.Status.Targets,row=>Assert.False(row.StoredCorrespondence));Assert.Equal(api.Accepts[0],controller.LastReceipt!.CommandId);
    }
    [Fact]
    public async Task Same_original_retry_after_unknown_never_prepares_another_copy_even_after_expiry()
    {
        var f=new WorkspaceTaskFixture();var api=new ChecklistConversionFixture("create"){LoseAcceptance=true};using var c=new WorkspaceConversionController(f.Session,api);await c.LoadAsync(api.Source.Id);
        foreach(var row in c.Drafts){var item=api.Template.Items.Single(item=>item.SourceItemId==row.SourceItemId);row.Mode=item.Mode;row.DesiredState=item.DesiredState;row.Notes=item.Notes??"";}c.Difficulty=.1;c.Attribute=HabiticaTaskAttribute.Int;
        await c.PrepareAsync();c.AcknowledgePage(true);await c.ConfirmAsync();Assert.True(c.AcceptanceUnknown);var id=c.Preview!.PreparedCommandId;
        await c.PrepareAsync();Assert.Single(api.Requests);await c.ConfirmAsync();Assert.Equal([id,id],api.Accepts);Assert.Equal(9,api.State.Document.Tasks.Count);Assert.Empty(api.State.Document.Completions);Assert.Equal(2,c.Status!.Targets.Count(row=>row.RequiresHistoricalMatch));Assert.Equal(8,c.Status.Targets.Sum(row=>row.PendingCommandIds.Count));
    }
    [Fact]
    public async Task Definitive_source_change_allows_new_read_but_existing_conversion_never_redirects_bindings()
    {
        var f=new WorkspaceTaskFixture();var api=new ChecklistConversionFixture {RejectPreparation=true};using var c=new WorkspaceConversionController(f.Session,api);await c.LoadAsync(api.Source.Id);api.ChooseReuse(c);await c.PrepareAsync();Assert.True(c.PreparationRejected);c.CancelReview();Assert.False(c.HasPendingIntent);
        api.RejectPreparation=false;await c.LoadAsync(api.Source.Id);api.ChooseReuse(c);await c.PrepareAsync();c.AcknowledgePage(true);await c.ConfirmAsync();
        var source=api.Source;api.State=api.State with {Document=api.State.Document with {Revision=api.State.Document.Revision+1,Tasks=api.State.Document.Tasks.Select(task=>task.Id==source.Id?task with {Revision=api.State.Document.Revision+1,Notes="Changed source"}:task).ToArray()}};
        await c.LoadAsync(source.Id);Assert.False(c.Status!.SourceUnchanged);Assert.Equal(source.Notes,c.Status.Conversion!.SourceSnapshot.Notes);await c.PrepareAsync();Assert.Contains("source changed",c.Message,StringComparison.OrdinalIgnoreCase);Assert.Equal(2,api.Requests.Count);
    }
    [Fact]
    public async Task Expired_fresh_review_cannot_accept_but_confirmed_receipt_survives_failed_status_refresh()
    {
        var f=new WorkspaceTaskFixture();var api=new ChecklistConversionFixture();using var c=new WorkspaceConversionController(f.Session,api);await c.LoadAsync(api.Source.Id);api.ChooseReuse(c);api.LosePreparation=true;await c.PrepareAsync();
        api.Preview=api.Preview! with {ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1),Expired=true};await c.RetryPreparationAsync();c.AcknowledgePage(true);Assert.False(c.CanConfirm);await c.ConfirmAsync();Assert.Empty(api.Accepts);
        api.Preview=api.Preview with {ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(10),Expired=false};await c.RetryPreparationAsync();api.FailStatus=true;await c.ConfirmAsync();Assert.NotNull(c.LastReceipt);Assert.Null(c.Status);Assert.False(c.HasPendingIntent);Assert.Contains("was saved",c.Message);
    }
    [Fact]
    public async Task Account_switch_discards_late_response_and_private_drafts()
    {
        var f=new WorkspaceTaskFixture();var api=new ChecklistConversionFixture();using var c=new WorkspaceConversionController(f.Session,api);await c.LoadAsync(api.Source.Id);api.ChooseReuse(c);c.Difficulty=2;c.Attribute=HabiticaTaskAttribute.Int;
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);api.ReadGate=async()=>{entered.SetResult();await release.Task;};var read=c.LoadAsync(api.Source.Id);await entered.Task;f.Session.SetState(SessionViewModel.Empty);release.SetResult();await read;
        Assert.Null(c.Status);Assert.Empty(c.Drafts);Assert.Null(c.Request);Assert.Null(c.SourceTaskId);Assert.Null(c.Difficulty);Assert.Null(c.Attribute);
    }
}
