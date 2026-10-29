using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Habitica.WebApp.Tests.Qualification;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
namespace Habitica.WebApp.Tests.Workspace;

public sealed class WorkspaceSourceRetirementTests:BunitContext
{
    private async Task<(WorkspaceTaskFixture Owner,SourceRetirementFixture Api,WorkspaceSourceRetirementController Controller)> Setup()
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var owner=new WorkspaceTaskFixture();var api=new SourceRetirementFixture();var commands=new WorkspaceTaskController(owner.Session,api,owner.Database);var todo=new TodoWorkspacePresentation(owner.Session,api,owner.Database,commands);var controller=new WorkspaceSourceRetirementController(owner.Session,api);
        Services.AddSingleton<IAppSessionController>(owner.Session);Services.AddSingleton<IWorkspaceCloudClient>(api);Services.AddSingleton(commands);Services.AddSingleton(todo);Services.AddSingleton(controller);await commands.LoadAsync(true);await todo.RefreshSourcesAsync();await todo.LoadSelectedAsync();return(owner,api,controller);
    }
    private static void Save(string name,string markup)=>PageLayoutEvidence.Save(name,markup,"/todo/retire/{ConversionId:guid}",name,["form","confirmation","disabled-controls","navigation","error"],nameof(WorkspaceSourceRetirementTests));
    [Fact]
    public async Task Full_replacement_review_and_separate_irreversible_approval_precede_exact_cleanup()
    {
        var(_,api,c)=await Setup();var cut=Render<TodoRetirementPage>(p=>p.Add(x=>x.ConversionId,api.Plan.ConversionId));Save("retirement-draft",cut.Markup);
        cut.Find("[data-testid='retirement-prepare']").Click();Assert.Single(api.Requests);Save("retirement-verifying",cut.Markup);cut.Find("[data-testid='retirement-verify']").Click();cut.WaitForAssertion(()=>Assert.Equal("ready",c.View!.State));Assert.False(c.CanAccept);Save("retirement-ready-long",cut.Markup);
        Assert.Contains(api.Plan.Source.Notes!,cut.Markup);Assert.Contains("Independent existing notes 9",cut.Markup);cut.Find("[data-testid='retirement-page-ack']").Change(true);Assert.False(c.CanAccept);cut.Find("[data-testid='retirement-irreversible-ack']").Change(true);Assert.True(c.CanAccept);api.LoseReply=true;cut.Find("[data-testid='retirement-confirm']").Click();cut.WaitForAssertion(()=>Assert.True(c.Unknown));Assert.Single(api.Accepts);Assert.False(c.CanAccept);Save("retirement-unknown",cut.Markup);
        c.ClearUnsentReview();Assert.NotNull(c.View);await c.AcceptAsync();Assert.Single(api.Accepts);var command=c.OriginalCommandId;cut.Dispose();var resumed=Render<TodoRetirementPage>(p=>p.Add(x=>x.ConversionId,api.Plan.ConversionId));Assert.Contains(command!.Value.ToString(),resumed.Markup);resumed.Find("[data-testid='retirement-recover']").Click();resumed.WaitForAssertion(()=>Assert.NotNull(c.View!.Receipt));Save("retirement-confirmed-original",resumed.Markup);Assert.True(WorkspaceJournalValidation.IsValid(api.State));Assert.Single(api.Accepts);
    }
    [Fact]
    public async Task Absent_source_expiry_refusal_and_unresolved_preparation_remain_explicit()
    {
        var(_,api,c)=await Setup();api.AlreadyAbsent=true;api.LosePreparation=true;var cut=Render<TodoRetirementPage>(p=>p.Add(x=>x.ConversionId,api.Plan.ConversionId));cut.Find("[data-testid='retirement-prepare']").Click();Save("retirement-unresolved-preparation",cut.Markup);c.ClearUnsentReview();Assert.NotNull(c.Request);cut.Find("[data-testid='retirement-prepare-recover']").Click();Assert.Equal(api.Requests[0],api.Requests[1]);api.FailVerification=true;await c.VerifyAsync();Assert.Empty(api.Accepts);Save("retirement-verification-refused",cut.Markup);api.FailVerification=false;await c.VerifyAsync();Save("retirement-ready-already-absent",cut.Markup);c.AcknowledgePage(true);c.ApproveIrreversible(true);Assert.True(c.CanAccept);
        api.View=api.View! with {Expired=true,ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1)};await c.RecoverAsync();Assert.False(c.CanAccept);await c.AcceptAsync();Assert.Empty(api.Accepts);Save("retirement-expired",cut.Markup);
        api.View=api.View with {Expired=false,ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(10)};await c.RecoverAsync();c.ApproveIrreversible(true);await c.AcceptAsync();Assert.Equal(SourceRetirementOutcome.AlreadyAbsent,c.View!.Receipt!.Outcome);Save("retirement-confirmed-already-absent",cut.Markup);
    }
    [Fact]
    public async Task Account_change_discards_private_data_and_late_preparation()
    {
        var(owner,api,c)=await Setup();var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);api.Gate=async()=>{entered.SetResult();await release.Task;};var prepare=c.PrepareAsync(api.Plan.ConversionId,api.Plan.Revision);await entered.Task;owner.Session.SetState(SessionViewModel.Empty);release.SetResult();await prepare;Assert.Null(c.View);Assert.Null(c.Request);Assert.Null(c.OriginalCommandId);Assert.False(c.IsBusy);Assert.False(c.CanAccept);
    }
}
