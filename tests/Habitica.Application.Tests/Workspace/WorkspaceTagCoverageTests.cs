using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Tests.Workspace;
public sealed class WorkspaceTagCoverageTests
{
    private static WorkspaceDocument Document(string name="all-six-kinds-same-names-distinct-identities")
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","tag-projection-v1.json")));
        return WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("cases").EnumerateArray().Single(row=>row.GetProperty("name").GetString()==name).GetProperty("document").GetRawText());
    }
    [Fact]
    public void Exact_owner_and_identity_evidence_change_counts_without_name_based_coverage()
    {
        var document=Document();var before=WorkspaceJson.Serialize(document);var account=document.TagLedger!.AccountId;
        var covered=WorkspaceHabiticaCoveragePolicy.Audit(document,account);
        Assert.Equal(6,covered.StoredGroupTagLinks);Assert.Equal(2,covered.ConfirmedRecordedMemberships);Assert.True(covered.HasCompleteStoredCoverage);
        Assert.Equal(0,covered.MissingGroupTagLinks);Assert.Single(covered.Tasks);
        foreach(var owner in new Guid?[]{null,Guid.NewGuid()})
        {
            var refused=WorkspaceHabiticaCoveragePolicy.Audit(document,owner);Assert.Equal(6,refused.GroupTagLinksNeedingReview);Assert.Equal(0,refused.StoredGroupTagLinks);Assert.Equal(0,refused.ConfirmedRecordedMemberships);Assert.All(refused.RequiredTags,row=>Assert.Null(row.HabiticaTagId));
        }
        var missing=WorkspaceHabiticaCoveragePolicy.Audit(document with { TagLedger=null },account);
        Assert.Equal(6,missing.MissingGroupTagLinks);Assert.Equal(0,missing.StoredGroupTagLinks);Assert.Equal(before,WorkspaceJson.Serialize(document));
    }
    [Fact]
    public void Pending_unknown_archived_and_stale_membership_remain_separate()
    {
        var pending=Document("pending-create-and-membership");var account=pending.TagLedger!.AccountId;
        var audit=WorkspaceHabiticaCoveragePolicy.Audit(pending,account);Assert.Equal(1,audit.PendingGroupTagLinks);Assert.Equal(5,audit.StoredGroupTagLinks);Assert.Contains(audit.RequiredTags,row=>row.MembershipState==HabiticaCoverageState.NeedsReview);
        var unknown=WorkspaceHabiticaCoveragePolicy.Audit(Document("unknown-create-no-replay-proof"),account);Assert.Equal(1,unknown.GroupTagLinksNeedingReview);Assert.Equal(2,unknown.ConfirmedRecordedMemberships);
        var archived=WorkspaceHabiticaCoveragePolicy.Audit(Document("archived-metadata-retained"),account);Assert.Contains(archived.RequiredTags,row=>row.Archived&&row.State==HabiticaCoverageState.Retained);Assert.Equal(2,archived.ConfirmedRecordedMemberships);
        var document=Document();var stale=WorkspaceHabiticaCoveragePolicy.Audit(document with { Revision=6 },account);Assert.Equal(6,stale.StoredGroupTagLinks);Assert.Equal(2,stale.ConfirmedRecordedMemberships);
        var historical=WorkspaceHabiticaCoveragePolicy.Audit(Document("historical-intent-keeps-original-tag"),account);Assert.Equal(6,historical.StoredGroupTagLinks);Assert.Equal(2,historical.ConfirmedRecordedMemberships);
        var changed=document with { Projections=[document.Projections[0] with { HabiticaTaskId=Guid.NewGuid().ToString("D") }] };
        Assert.Equal(0,WorkspaceHabiticaCoveragePolicy.Audit(changed,account).ConfirmedRecordedMemberships);
    }
    [Fact]
    public void Partial_or_ambiguous_member_receipts_never_confirm_the_whole_selection()
    {
        var partial=Document("partial-membership-keeps-original-member-receipt");var ledger=partial.TagLedger!;var members=ledger.Intents[0].Members!;
        Assert.Equal(WorkspaceTagState.Confirmed,members[0].State);Assert.NotNull(members[0].Evidence);
        Assert.Equal(WorkspaceTagState.Pending,members[1].State);Assert.Null(members[1].Evidence);
        var audit=WorkspaceHabiticaCoveragePolicy.Audit(partial,ledger.AccountId);Assert.Equal(6,audit.StoredGroupTagLinks);Assert.Equal(1,audit.ConfirmedRecordedMemberships);Assert.Contains(audit.RequiredTags,row=>row.EntityId==ledger.Mappings[0].EntityId&&row.MembershipState==HabiticaCoverageState.NeedsReview);
        var document=Document();var original=document.TagLedger!.Intents[0];
        var ambiguous=document with { TagLedger=document.TagLedger with { Intents=[..document.TagLedger.Intents,original with { Id=Guid.NewGuid(),CommandId=Guid.NewGuid() }] } };
        Assert.Empty(WorkspaceRules.Validate(ambiguous));var refused=WorkspaceHabiticaCoveragePolicy.Audit(ambiguous,document.TagLedger.AccountId);
        Assert.Equal(2,refused.ConfirmedRecordedMemberships);Assert.Contains(refused.RequiredTags,row=>row.EntityId==document.TagLedger.Mappings[0].EntityId&&row.MembershipState==HabiticaCoverageState.NeedsReview);
    }

    [Fact]
    public void Complete_canonical_placement_confirms_all_six_scopes_across_unrelated_revisions()
    {
        var document=Document();var task=document.Tasks.Single();document=document with {Tasks=[task with {ProjectId=document.Projects[0].Id,SectionId=document.Sections[0].Id,LabelIds=[document.Labels[0].Id]}]};
        Assert.Empty(WorkspaceRules.Validate(document));var account=document.TagLedger!.AccountId;
        Assert.Equal(6,WorkspaceHabiticaCoveragePolicy.Audit(document,account).ConfirmedRecordedMemberships);
        var later=document with {Revision=6,Tasks=[document.Tasks[0] with {Revision=6,Text="Independent text",Priority=CompanionPriority.High,Deadline=new DateOnly(2030,12,31)}]};
        var audit=WorkspaceHabiticaCoveragePolicy.Audit(later,account);Assert.Equal(6,audit.ConfirmedRecordedMemberships);Assert.True(audit.HasCompleteStoredCoverage);
    }
    [Fact]
    public void Logical_removal_retains_tag_identity_and_old_intents_without_authorizing_writes()
    {
        var document=Document();var account=document.TagLedger!.AccountId;var original=WorkspaceJson.Serialize(document);
        var project=document.Projects[0];var command=new WorkspaceOrganizationCommand(Guid.NewGuid(),document.Id,document.Revision,WorkspaceOrganizationKind.Project,WorkspaceOrganizationOperation.Remove,project.Id,new(Removal:WorkspaceProjectRemoval.From(document,project.Id)));
        var result=WorkspaceOrganizationEngine.Accept(new(document,5,[],[]),command,DateTimeOffset.Parse("2026-10-21T00:00:00Z"));
        Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);var next=result.Snapshot!.Document;
        Assert.True(next.Projects[0].IsRemoved);Assert.Equal(document.TagLedger,next.TagLedger);Assert.Empty(result.Snapshot.Commands);Assert.Empty(result.Snapshot.Events!);
        Assert.Single(next.OrganizationHistory!);Assert.Equal(WorkspaceOrganizationCommands.Hash(command),next.OrganizationHistory![0].PayloadHash);
        var audit=WorkspaceHabiticaCoveragePolicy.Audit(next,account);Assert.Contains(audit.RequiredTags,row=>row.EntityId==project.Id&&row.State==HabiticaCoverageState.NeedsReview);Assert.Equal(2,audit.ConfirmedRecordedMemberships);
        Assert.Equal(original,WorkspaceJson.Serialize(document));
    }
}
