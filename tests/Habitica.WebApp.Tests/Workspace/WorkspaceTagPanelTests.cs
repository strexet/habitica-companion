using Bunit;
using Habitica.Application.Workspace;
using Habitica.Application.Integration;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Habitica.WebApp.Tests.Qualification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class WorkspaceTagPanelTests : BunitContext
{
    private sealed class Client : WorkspaceTaskFixture.Cloud,IWorkspaceCloudClient,IWorkspaceTagApi
    {
        public Guid EntityId=Guid.NewGuid(),NativeId=Guid.NewGuid(),WorkId=Guid.NewGuid();public int ConsentReads,TagStatusReads,NativeReads,RecoveryReads;public bool Consent,Unknown,FailAccept,Terminal,Prepared,KnownRefusal;
        public List<bool> ConsentChanges=[];public List<WorkspaceTagRequest> Requests=[];public List<Guid> Accepts=[];public int TaskCount=1;
        public Func<Task<IntegrationTagConsentResult>>? DelayedConsent {get;set;}
        public Task<IntegrationTagConsentResult> ReadTagConsentAsync(CancellationToken token){ConsentReads++;return DelayedConsent?.Invoke()??Task.FromResult(new IntegrationTagConsentResult(true,"Checked",Consent));}
        public Task<IntegrationTagConsentResult> SetTagConsentAsync(bool enabled,CancellationToken token){ConsentChanges.Add(enabled);Consent=enabled;return Task.FromResult(new IntegrationTagConsentResult(true,"Changed",enabled));}
        public Task<WorkspaceTaskResponse<WorkspaceTagStatusPage>> ReadGroupTagsAsync(int offset,long? revision,CancellationToken token)
        {TagStatusReads++;return Task.FromResult(new WorkspaceTaskResponse<WorkspaceTagStatusPage>(true,new("stored-canonical-evidence",WorkspaceId!.Value,State.Document.Revision,true,Consent,true,false,new(1,1,0,0,0,0),new(1,0,0,0,1),[new(EntityId,WorkspaceOrganizationKind.Project,"Long duplicate project "+new string('W',170),false,false,"missing","missing",null,TaskCount,0,null,Unknown?[WorkId]:[])],1,null),"Coverage checked"));}
        public Task<WorkspaceTaskResponse<WorkspaceTagCandidatePage>> InspectTagCandidatesAsync(WorkspaceOrganizationKind kind,Guid id,int offset,string? hash,CancellationToken token)
        {NativeReads++;return Task.FromResult(new WorkspaceTaskResponse<WorkspaceTagCandidatePage>(true,new(WorkspaceId!.Value,State.Document.Revision,id,kind,"Duplicate project",new string('a',64),[new(Guid.NewGuid(),"Same name",false),new(NativeId,"Same name",false)],2,null),"Choose exact ID"));}
        public Task<WorkspaceTaskResponse<WorkspaceTagReview>> PrepareTagReviewAsync(WorkspaceTagRequest request,CancellationToken token)
        {
            Requests.Add(request);var changes=request.Operation=="reconcile"?Enumerable.Range(0,TaskCount).Select(i=>new WorkspaceTagTaskChange(Guid.NewGuid(),NativeId.ToString(),true,"pending",null,"Task "+i+" "+new string('x',120))).ToArray():[];
            var details=new WorkspaceTagReviewDetails(EntityId,WorkspaceOrganizationKind.Project,"Duplicate project",request.HabiticaTagId?.ToString(),changes,"Review exact metadata. No game effects.",OriginalWorkId:request.Operation=="recover"?WorkId:null,OriginalAttemptId:request.Operation=="recover"?Guid.NewGuid():null,ObservedTag:request.Operation=="adopt"?new(NativeId,"Same name"):null);
            OriginalReview=new(Guid.NewGuid(),request.RequestId,WorkspaceId!.Value,request.Operation,State.Document.Revision,DateTimeOffset.UtcNow.AddMinutes(5),true,0,details);
            return Task.FromResult(new WorkspaceTaskResponse<WorkspaceTagReview>(true,OriginalReview,"Prepared"));
        }
        public Task<WorkspaceTaskResponse<WorkspaceTagReceipt>> AcceptTagReviewAsync(WorkspaceTagReview review,CancellationToken token)
        {Accepts.Add(review.CommandId!.Value);return Task.FromResult(FailAccept?new WorkspaceTaskResponse<WorkspaceTagReceipt>(false,null,"Lost acknowledgement",!KnownRefusal):new(true,Receipt(review),"Saved"));}
        public WorkspaceTagReceipt Receipt(WorkspaceTagReview review)=>Terminal?new(review.CommandId!.Value,review.RequestId,WorkspaceId,review.Operation,false,0,false,[],ReviewOutcome:"terminal-unaccepted",Reason:"tag_preview_changed",ClosedAtUtc:DateTimeOffset.UtcNow):Prepared?new(review.CommandId!.Value,review.RequestId,WorkspaceId,review.Operation,null,0,true,[],Prepared:true,ReviewOutcome:"prepared"):new(review.CommandId!.Value,review.RequestId,WorkspaceId,review.Operation,true,0,false,[WorkId]);
        public WorkspaceTagReview? OriginalReview {get;set;}
        public Task<WorkspaceTaskResponse<WorkspaceTagReceipt>> ReadTagReviewAsync(Guid id,CancellationToken token)=>Task.FromResult(new WorkspaceTaskResponse<WorkspaceTagReceipt>(true,Receipt(OriginalReview!),"Original receipt checked"));
        public Task<WorkspaceTaskResponse<WorkspaceTagOriginal>> RecoverTagReceiptAsync(Guid id,CancellationToken token)
        {RecoveryReads++;return Task.FromResult(new WorkspaceTaskResponse<WorkspaceTagOriginal>(true,new(id,Guid.NewGuid(),1,Unknown?"unknownOutcome":"confirmed",Unknown?"outcome_unknown":null,EntityId,WorkspaceOrganizationKind.Project,"ensureTag",Guid.NewGuid(),"original-tag-work",Unknown,0),"Original receipt only"));}
    }
    private Client Setup()
    {
        var f=new WorkspaceTaskFixture();var client=new Client {Connected=true};Services.AddMudServices();Services.AddSingleton<IJSRuntime>(f.Js);Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton<IWorkspaceCloudClient>(client);Services.AddSingleton(f.Database);Services.AddSingleton(new WorkspaceDataController(f.Session,client,f.Database));
        Services.AddSingleton(new WorkspaceTaskController(f.Session,client,f.Database));Services.AddSingleton(new PushDeviceController(f.Session,new PushReminderFixture.Client(),new PushReminderFixture.Browser(),new PushReminderFixture.Store()));Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton<TodoWorkspacePresentation>();JSInterop.Mode=JSRuntimeMode.Loose;return client;
    }
    private static void Save(string name,string markup,string route="/todo/sync")=>PageLayoutEvidence.Save(name,markup,route,name,["form","confirmation","disabled-controls","navigation"],nameof(WorkspaceTagPanelTests));
    [Fact]
    public void Consent_is_explicit_status_is_unverified_and_no_automatic_server_reads_or_grants_occur()
    {
        var client=Setup();var cut=Render<WorkspaceTagPanel>();Assert.Contains("unverified",cut.Find("[data-testid='tag-consent-status']").TextContent);Assert.Equal(0,client.ConsentReads);Assert.Equal(0,client.TagStatusReads);Assert.Empty(client.ConsentChanges);Save("tags-unverified",cut.Markup,"/settings");
        cut.Find("[data-testid='tag-consent-check']").Click();Assert.Equal(1,client.ConsentReads);Assert.True(cut.Find("[data-testid='tag-consent-enable']").HasAttribute("disabled"));cut.Find("[data-testid='tag-consent-disclosure']").Change(true);cut.Find("[data-testid='tag-consent-enable']").Click();Assert.Equal([true],client.ConsentChanges);Assert.Equal(1,client.TagStatusReads);Assert.Contains("enabled",cut.Find("[data-testid='tag-consent-status']").TextContent);Save("tags-enabled",cut.Markup);
        cut.Find("[data-testid='tag-consent-pause']").Click();Assert.Equal([true,false],client.ConsentChanges);Assert.Empty(client.Accepts);
    }
    [Fact]
    public void Complete_membership_requires_every_page_before_confirmation_and_reports_zero_game_effects()
    {
        var client=Setup();client.Consent=true;client.TaskCount=45;var cut=Render<WorkspaceTagPanel>();cut.Find("[data-testid='tag-coverage-check']").Click();cut.Find($"[data-testid='tag-reconcile-{client.EntityId:D}']").Click();Assert.Single(client.Requests);Assert.True(cut.Find("[data-testid='tag-confirm']").HasAttribute("disabled"));Save("tags-full-review",cut.Markup);
        for(var page=0;page<3;page++){cut.Find("[data-testid='tag-membership-ack']").Change(true);if(page<2){Assert.True(cut.Find("[data-testid='tag-confirm']").HasAttribute("disabled"));cut.FindAll("button").Single(button=>button.TextContent=="Next task changes").Click();}}
        Assert.False(cut.Find("[data-testid='tag-confirm']").HasAttribute("disabled"));cut.Find("[data-testid='tag-confirm']").Click();Assert.Single(client.Accepts);Assert.Contains("Game effects: 0",cut.Find("[data-testid='tag-receipt']").TextContent);Save("tags-queued",cut.Markup);
    }
    [Fact]
    public void Duplicate_names_open_exact_adoption_review_and_do_not_auto_accept()
    {
        var client=Setup();client.Consent=true;var cut=Render<WorkspaceTagPanel>();cut.Find("[data-testid='tag-coverage-check']").Click();cut.Find($"[data-testid='tag-candidates-{client.EntityId:D}']").Click();Assert.Equal(1,client.NativeReads);Assert.Equal(2,cut.FindAll("[data-testid^='tag-adopt-']").Count);Save("tags-duplicate-candidates",cut.Markup);
        cut.Find($"[data-testid='tag-adopt-{client.NativeId:D}']").Click();Assert.Equal(client.NativeId,Assert.Single(client.Requests).HabiticaTagId);Assert.Contains(client.NativeId.ToString(),cut.Find("[data-testid='tag-review']").TextContent);Assert.Empty(client.Accepts);Save("tags-adopt-review",cut.Markup);
    }
    [Fact]
    public void Unknown_acceptance_blocks_replacement_and_recovers_original_review_without_resending()
    {
        var client=Setup();client.Consent=true;client.FailAccept=true;client.TaskCount=0;var cut=Render<WorkspaceTagPanel>();cut.Find("[data-testid='tag-coverage-check']").Click();cut.Find($"[data-testid='tag-reconcile-{client.EntityId:D}']").Click();
        cut.Find("[data-testid='tag-confirm']").Click();Assert.Single(client.Accepts);Assert.False(cut.Find("[data-testid='tag-confirm']").HasAttribute("disabled"));Assert.Contains("Retry original tag acceptance",cut.Markup);Assert.Contains("Acceptance is uncertain",cut.Markup);Save("tags-acceptance-unknown",cut.Markup);Assert.Single(client.Requests);
        cut.Find("[data-testid='tag-review-recover']").Click();Assert.Single(client.Accepts);Assert.Single(client.Requests);Assert.Contains("Game effects: 0",cut.Markup);
    }
    [Fact]
    public void Generic_first_refusal_cannot_release_original_acceptance_fence()
    {
        var client=Setup();client.Consent=true;client.FailAccept=true;client.KnownRefusal=true;client.TaskCount=0;var cut=Render<WorkspaceTagPanel>();cut.Find("[data-testid='tag-coverage-check']").Click();cut.Find($"[data-testid='tag-reconcile-{client.EntityId:D}']").Click();cut.Find("[data-testid='tag-confirm']").Click();
        Assert.Contains("Acceptance is uncertain",cut.Markup);Assert.True(cut.FindAll("button").Single(button=>button.TextContent=="Cancel tag review").HasAttribute("disabled"));Assert.Single(client.Requests);
    }
    [Fact]
    public void Sticky_unknown_requires_terminal_original_proof_before_fresh_review()
    {
        var client=Setup();client.Consent=true;client.FailAccept=true;client.TaskCount=0;var cut=Render<WorkspaceTagPanel>();cut.Find("[data-testid='tag-coverage-check']").Click();cut.Find($"[data-testid='tag-reconcile-{client.EntityId:D}']").Click();
        cut.Find("[data-testid='tag-confirm']").Click();client.KnownRefusal=true;cut.Find("[data-testid='tag-confirm']").Click();Assert.Contains("Acceptance is uncertain",cut.Markup);
        client.Prepared=true;cut.Find("[data-testid='tag-review-recover']").Click();Assert.Contains("Acceptance is uncertain",cut.Markup);Assert.Single(client.Requests);
        client.Terminal=true;cut.Find("[data-testid='tag-review-recover']").Click();Assert.Empty(cut.FindAll("[data-testid='tag-review']"));Assert.Contains("was not accepted and is closed",cut.Markup);Save("tags-terminal-unaccepted",cut.Markup);
        Assert.False(cut.Find($"[data-testid='tag-reconcile-{client.EntityId:D}']").HasAttribute("disabled"));cut.Find($"[data-testid='tag-reconcile-{client.EntityId:D}']").Click();Assert.Equal(2,client.Requests.Count);Assert.NotEqual(client.Requests[0].RequestId,client.Requests[1].RequestId);Assert.Equal(2,client.Accepts.Count);
        Save("tags-fresh-replacement-review",cut.Markup);
    }
    [Fact]
    public void Original_unknown_receipt_offers_separate_observation_review_without_an_automatic_mutation()
    {
        var client=Setup();client.Consent=true;client.Unknown=true;var cut=Render<WorkspaceTagPanel>();cut.Find("[data-testid='tag-coverage-check']").Click();cut.FindAll("button").Single(button=>button.TextContent=="Check original tag result").Click();Assert.Equal(1,client.RecoveryReads);Assert.Empty(client.Requests);Assert.Empty(client.Accepts);Save("tags-original-unknown",cut.Markup);
        cut.Find("[data-testid='tag-observation-review']").Click();Assert.Equal("recover",Assert.Single(client.Requests).Operation);Assert.Equal(client.WorkId,client.Requests[0].WorkId);Assert.Empty(client.Accepts);Save("tags-observation-review",cut.Markup);
    }
    [Fact]
    public async Task Late_consent_result_cannot_rebind_a_changed_workspace()
    {
        var client=Setup();var release=new TaskCompletionSource<IntegrationTagConsentResult>(TaskCreationOptions.RunContinuationsAsynchronously);client.DelayedConsent=()=>release.Task;
        var cut=Render<WorkspaceTagPanel>();var pending=cut.Find("[data-testid='tag-consent-check']").ClickAsync(new());
        await Task.Yield();client.State=client.State with { Document=client.State.Document with { Id=Guid.NewGuid() } };cut.Render();
        release.SetResult(new(true,"OLD ACCOUNT SECRET",true));await pending;
        Assert.DoesNotContain("OLD ACCOUNT SECRET",cut.Markup);Assert.Contains("unverified",cut.Find("[data-testid='tag-consent-status']").TextContent);Assert.Empty(client.ConsentChanges);Save("tags-changed-account",cut.Markup);
    }
    [Fact]
    public void Unknown_acceptance_retry_reuses_the_original_command_without_a_new_preview()
    {
        var client=Setup();client.Consent=true;client.FailAccept=true;client.TaskCount=0;var cut=Render<WorkspaceTagPanel>();cut.Find("[data-testid='tag-coverage-check']").Click();cut.Find($"[data-testid='tag-reconcile-{client.EntityId:D}']").Click();
        cut.Find("[data-testid='tag-confirm']").Click();var original=Assert.Single(client.Accepts);client.FailAccept=false;cut.Find("[data-testid='tag-confirm']").Click();Assert.Equal([original,original],client.Accepts);Assert.Single(client.Requests);Assert.Contains("Game effects: 0",cut.Markup);
    }
    [Fact]
    public void Shared_command_tag_review_requires_every_reference_page_and_resets_for_new_identity()
    {
        Setup();var refs=Enumerable.Range(0,41).Select(_=>Guid.NewGuid()).ToArray();var group=new WorkspaceTagGroupChange(Guid.NewGuid(),WorkspaceOrganizationKind.Project,"Shared project",false,false,refs,true,[]);
        var preview=new WorkspaceTagChangePreview(true,true,true,[group with { RetainMembership=false }]);var reviewed=false;var commandId=Guid.NewGuid();
        var cut=Render<WorkspaceTagChangeReview>(p=>p.Add(x=>x.CommandId,commandId).Add(x=>x.Preview,preview).Add(x=>x.Reviewed,value=>reviewed=value));Assert.False(reviewed);
        for(var page=0;page<3;page++){cut.Find("[data-testid='tag-command-ack']").Change(true);if(page<2){Assert.False(reviewed);cut.FindAll("button").Single(button=>button.TextContent=="Next tag references").Click();}}
        Assert.True(reviewed);Save("tags-command-membership",cut.Markup,"/todo/projects");cut.Render(p=>p.Add(x=>x.CommandId,Guid.NewGuid()));Assert.False(reviewed);Assert.False(cut.Find("[data-testid='tag-command-ack']").HasAttribute("checked"));
        cut.Render(p=>p.Add(x=>x.CommandId,Guid.NewGuid()).Add(x=>x.Preview,preview with {ScopeAllowed=false}));Assert.True(reviewed);Assert.Contains("separate permission",cut.Markup);Save("tags-command-old-scope",cut.Markup,"/todo/tasks");
    }
    [Fact]
    public void Todo_sync_route_renders_connection_controls_and_group_tags_together()
    {Setup();var cut=Render<Habitica.WebApp.Pages.TodoSyncPage>();Assert.NotNull(cut.Find("[data-testid='workspace-tags']"));Assert.Contains("Todo Sync",cut.Markup);Save("tags-sync-page",cut.Markup);}
}
