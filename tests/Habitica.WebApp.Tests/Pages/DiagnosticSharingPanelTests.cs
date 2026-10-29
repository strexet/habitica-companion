using Bunit;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
using Habitica.WebApp.Components;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
namespace Habitica.WebApp.Tests.Pages;
public sealed class DiagnosticSharingPanelTests:BunitContext
{
    protected override void Dispose(bool disposing){if(disposing){DisposeAsync().AsTask().GetAwaiter().GetResult();return;}base.Dispose(disposing);}
    private (DiagnosticSharingFake api,DiagnosticSharingController sharing,FakeAppSessionController session) Setup(int count=1)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();
        var entries=Enumerable.Range(0,count).Select(i=>new DiagnosticsLogEntry("event-"+i,DateTimeOffset.UtcNow.AddSeconds(-i),DiagnosticsFeatureArea.Workspace,"workspace-tag-accept",DiagnosticsSeverity.Warning,DiagnosticsMode.LiveMutation,"private-task-canary",new Dictionary<string,string>{{"apiToken","private-token-canary"}})).ToArray();
        var session=new FakeAppSessionController(SessionViewModel.Empty with {IsAuthenticated=true,UserId=Guid.NewGuid().ToString(),DiagnosticsLogEntries=entries});var api=new DiagnosticSharingFake();var sharing=new DiagnosticSharingController(session,api,TimeProvider.System);Services.AddSingleton<IAppSessionController>(session);Services.AddSingleton(sharing);return(api,sharing,session);
    }
    private void Save(IRenderedComponent<LiveTestsPage> cut,string name,string state)
    {
        Qualification.PageLayoutEvidence.Save(name,cut.Markup,"/diagnostics",state,["diagnostic-sharing","consent","cloud-preview","original-receipt"],nameof(DiagnosticSharingPanelTests));
        Qualification.PageLayoutEvidence.Save(name+"-alias",cut.Markup,"/live-tests",state,["diagnostic-sharing","consent","cloud-preview","original-receipt"],nameof(DiagnosticSharingPanelTests));
    }
    [Fact]
    public async Task Consent_preview_metadata_and_upload_are_separate_explicit_actions()
    {
        var (api,sharing,_)=Setup(2);var cut=Render<LiveTestsPage>();Assert.Equal(0,api.Reads+api.Preparations+api.Uploads+api.ConsentCalls);Save(cut,"diagnostics-sharing-initial","unqueried-sharing");
        cut.Find("[data-testid=diagnostic-share-status]").Click();cut.WaitForAssertion(()=>Assert.NotNull(sharing.Status));Assert.True(cut.Find("[data-testid=diagnostic-share-consent]").HasAttribute("disabled"));Save(cut,"diagnostics-sharing-consent","separate-consent-disabled");
        cut.Find("[data-testid=diagnostic-share-consent-ack]").Change(true);cut.Find("[data-testid=diagnostic-share-consent]").Click();cut.WaitForAssertion(()=>Assert.True(api.Enabled));
        cut.Find("[data-testid=diagnostic-share-preview]").Click();Assert.Equal(2,sharing.Preview!.Entries.Count);Assert.Equal(0,sharing.Preview.OmittedMatchingCount);Assert.Equal(0,api.Preparations+api.Uploads);Assert.DoesNotContain("private-",cut.Find("[data-testid=diagnostic-cloud-preview]").TextContent);Save(cut,"diagnostics-sharing-preview","bounded-sanitized-preview");
        cut.Find("[data-testid=diagnostic-share-prepare]").Click();cut.WaitForAssertion(()=>Assert.NotNull(sharing.Original));Assert.Equal(1,api.Preparations);Assert.Equal(0,api.Uploads);Assert.True(cut.Find("[data-testid=diagnostic-share-upload]").HasAttribute("disabled"));Save(cut,"diagnostics-sharing-prepared","exact-metadata-review-locked-filters");
        cut.Find("[data-testid=diagnostic-share-ack]").Change(true);Save(cut,"diagnostics-sharing-ready","acknowledged-upload-enabled");cut.Find("[data-testid=diagnostic-share-upload]").Click();cut.WaitForAssertion(()=>Assert.NotNull(sharing.Original!.Receipt));Assert.Equal(1,api.Uploads);Assert.Equal(DiagnosticSharing.Serialize(sharing.Preview),DiagnosticSharing.Serialize(api.Uploaded!));Assert.False(sharing.Unresolved);Save(cut,"diagnostics-sharing-receipt","original-upload-receipt");
        await sharing.UploadAsync();Assert.Equal(1,api.Uploads);
    }
    [Fact]
    public void Local_preview_reports_exact_filter_and_omission_counts_without_network()
    {
        var (api,sharing,_)=Setup(105);sharing.SetFilters(new(Feature:DiagnosticsFeatureArea.Workspace,Severity:DiagnosticsSeverity.Warning));sharing.PreviewLocal();Assert.Equal(100,sharing.Preview!.Entries.Count);Assert.Equal(105,sharing.Preview.MatchingCount);Assert.Equal(5,sharing.Preview.OmittedMatchingCount);Assert.Equal(0,api.Reads+api.Preparations+api.Uploads+api.ConsentCalls);sharing.SetFilters(new(Severity:DiagnosticsSeverity.Error));Assert.Null(sharing.Preview);sharing.PreviewLocal();Assert.Empty(sharing.Preview!.Entries);Assert.Equal(105,sharing.Preview.AvailableCount);
    }
    [Fact]
    public async Task Lost_ack_and_missing_original_preserve_request_and_block_replacement_and_repeat_upload()
    {
        var (api,sharing,_)=Setup();api.Enabled=true;await sharing.RefreshAsync();sharing.PreviewLocal();await sharing.PrepareAsync();var request=sharing.RequestId;api.LoseAck=true;await sharing.UploadAsync();Assert.True(sharing.Unresolved);Assert.False(sharing.CanUpload);await sharing.UploadAsync();Assert.Equal(1,api.Uploads);sharing.PreviewLocal();Assert.Equal(request,sharing.RequestId);
        var cut=Render<LiveTestsPage>();Save(cut,"diagnostics-sharing-unknown","unknown-acceptance-original-retained");api.Missing=true;await cut.InvokeAsync(sharing.RecoverAsync);Assert.True(sharing.Unresolved);Assert.Contains("does not prove failure",sharing.Message);Assert.Equal(request,sharing.RequestId);Assert.True(cut.Find("[data-testid=diagnostic-share-preview]").HasAttribute("disabled"));
        api.Missing=false;await cut.InvokeAsync(sharing.RecoverAsync);Assert.True(sharing.Original!.Saved);Assert.Equal(1,api.Uploads);Assert.Equal(request,sharing.Original.RequestId);
    }
    [Fact]
    public async Task Quota_refusal_and_expiry_require_original_terminal_evidence_before_new_review()
    {
        var (api,sharing,_)=Setup();api.Enabled=true;await sharing.RefreshAsync();sharing.PreviewLocal();await sharing.PrepareAsync();api.Refuse=true;await sharing.UploadAsync();Assert.True(sharing.Unresolved);Assert.False(sharing.CanReplace);Assert.False(sharing.CanUpload);
        await sharing.RecoverAsync();Assert.True(sharing.Unresolved);Assert.False(sharing.CanUpload);
        api.Original=api.Original! with {State="expired",Saved=false,OutcomeUnknown=false};await sharing.RecoverAsync();Assert.False(sharing.Unresolved);sharing.PreviewLocal();Assert.Null(sharing.RequestId);
        await sharing.PrepareAsync();api.Original=api.Original! with {AcceptByUtc=DateTimeOffset.UtcNow.AddMinutes(-1)};await sharing.RecoverAsync();Assert.False(sharing.CanUpload);var cut=Render<LiveTestsPage>();Save(cut,"diagnostics-sharing-expired","expired-preparation");
    }
    [Fact]
    public async Task Revoke_cleanup_failure_and_unavailable_channel_keep_manual_sharing()
    {
        var (api,sharing,_)=Setup();api.Enabled=true;await sharing.RefreshAsync();sharing.PreviewLocal();await sharing.PrepareAsync();api.CleanupPending=true;await sharing.SetConsentAsync(false);Assert.False(sharing.Status!.Enabled);Assert.True(sharing.Status.CleanupPending);Assert.False(sharing.CanUpload);Assert.Null(sharing.Preview);var cut=Render<LiveTestsPage>();Save(cut,"diagnostics-sharing-cleanup","revoked-cleanup-unconfirmed");Assert.Contains("Deletion is not confirmed",cut.Markup);
        api.CleanupPending=false;await cut.InvokeAsync(()=>sharing.SetConsentAsync(true));api.ThrowConsent=true;await cut.InvokeAsync(()=>sharing.SetConsentAsync(false));Assert.Null(sharing.Status);Assert.False(sharing.CanUpload);
        api.DiagnosticSharingAvailable=false;cut.Render();Assert.Single(cut.FindAll("[data-testid=preview-diagnostics-bundle]"));Save(cut,"diagnostics-sharing-unavailable","manual-fallback");
    }
    [Fact]
    public async Task Inflight_acceptance_keeps_exact_request_and_disables_other_cloud_actions()
    {
        var (api,sharing,_)=Setup();api.Enabled=true;await sharing.RefreshAsync();sharing.PreviewLocal();await sharing.PrepareAsync();api.Held=new();var cut=Render<LiveTestsPage>();var pending=sharing.UploadAsync();cut.WaitForAssertion(()=>Assert.True(cut.Find("[data-testid=diagnostic-share-recover]").HasAttribute("disabled")));Save(cut,"diagnostics-sharing-inflight","upload-response-pending");Assert.True(sharing.Unresolved);Assert.Equal(1,api.Uploads);api.Held.SetResult(new(false,null,"unavailable",true));await pending;Assert.True(sharing.Unresolved);Assert.False(sharing.CanUpload);
    }
    [Fact]
    public async Task Navigation_preserves_frozen_preview_and_filters_but_identity_change_erases_private_state()
    {
        var (api,sharing,session)=Setup();sharing.SetFilters(new(Feature:DiagnosticsFeatureArea.Workspace));sharing.PreviewLocal();var exact=DiagnosticSharing.Serialize(sharing.Preview!);var cut=Render<DiagnosticSharingPanel>();cut.Dispose();var next=Render<DiagnosticSharingPanel>();Assert.Equal(exact,DiagnosticSharing.Serialize(sharing.Preview!));Assert.Equal(DiagnosticsFeatureArea.Workspace,sharing.Filters.Feature);Assert.Equal(0,api.Uploads+api.Preparations);
        api.Enabled=true;await next.InvokeAsync(sharing.RefreshAsync);await next.InvokeAsync(sharing.PrepareAsync);api.Held=new();var pending=sharing.UploadAsync();session.SetState(session.State with {UserId=Guid.NewGuid().ToString()});Assert.Null(sharing.Preview);Assert.Null(sharing.Original);Assert.Null(sharing.RequestId);api.Held.SetResult(new(true,api.Original,"ok"));await pending;Assert.Null(sharing.Original);Assert.False(sharing.Unresolved);
        sharing.PreviewLocal();api.DiagnosticIdentity="fixture-connection-2";sharing.EnsureIdentity();Assert.Null(sharing.Preview);Assert.Null(sharing.Status);
    }
}
