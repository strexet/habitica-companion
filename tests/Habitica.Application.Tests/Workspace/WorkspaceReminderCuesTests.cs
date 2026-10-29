using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Tests.Workspace;
public sealed class WorkspaceReminderCuesTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-26T09:00:00Z");
    [Fact]
    public void Canonical_read_retains_legacy_cues_and_excludes_archived_completed_cancelled_and_unscheduled_dates()
    {
        var task=new CompanionTask(Guid.NewGuid(),1,"Legacy reached cue",CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,Now,Now,ReviewAfterUtc:Now.AddDays(-1));
        var document=new WorkspaceDocument{Id=Guid.NewGuid(),Tasks=[task,task with{Id=Guid.NewGuid(),Text="Future",State=CompanionTaskState.Waiting,ReviewAfterUtc=Now.AddDays(1)},task with{Id=Guid.NewGuid(),IsArchived=true},task with{Id=Guid.NewGuid(),State=CompanionTaskState.Completed},task with{Id=Guid.NewGuid(),State=CompanionTaskState.Cancelled},task with{Id=Guid.NewGuid(),ReviewAfterUtc=null,Deadline=new(2026,10,26),PlannedDate=new(2026,10,26)}]};
        document=document with{Completions=[new(Guid.NewGuid(),1,document.Tasks.Single(t=>t.State==CompanionTaskState.Completed).Id,Guid.NewGuid(),Now)]};
        var before=WorkspaceJson.Serialize(document);var cues=WorkspaceReminderCues.Read(document);Assert.Equal(2,cues.Count);Assert.All(cues,c=>{Assert.Equal(0,c.ScheduleRevision);Assert.Equal("UTC",c.TimeZoneId);});Assert.Equal(before,WorkspaceJson.Serialize(document));
    }
    [Fact]
    public void Schedule_identity_changes_only_for_explicit_review_cue_actions_and_not_independent_stale_flags()
    {
        var snapshot=new WorkspaceJournalSnapshot(new WorkspaceDocument{Id=Guid.NewGuid()},1,[],[]);var id=Guid.NewGuid();
        snapshot=WorkspaceCommandEngine.Accept(snapshot,new(Guid.NewGuid(),snapshot.Document.Id,1,WorkspaceOperation.CreateTodo,id,new(Text:"Review")),Now).Snapshot!;
        WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot s,WorkspaceTaskReview review)=>WorkspaceCommandEngine.Accept(s,new(Guid.NewGuid(),s.Document.Id,s.Document.Revision,WorkspaceOperation.ReviewTask,id,new(Review:review)),Now).Snapshot!;
        snapshot=Apply(snapshot,new(TaskReviewAction.Set,"Asia/Novosibirsk",Now));var first=WorkspaceReminderCues.Read(snapshot.Document).Single();Assert.Equal(snapshot.Document.Revision,first.ScheduleRevision);
        snapshot=Apply(snapshot,new(TaskReviewAction.SetStale,"UTC",StaleSinceUtc:Now));Assert.Equal(first,WorkspaceReminderCues.Read(snapshot.Document).Single());
        snapshot=Apply(snapshot,new(TaskReviewAction.Snooze,"UTC",Now.AddDays(1)));var later=WorkspaceReminderCues.Read(snapshot.Document).Single();Assert.NotEqual(first.ScheduleRevision,later.ScheduleRevision);Assert.Equal(Now.AddDays(1),later.ReviewAfterUtc);
        snapshot=Apply(snapshot,new(TaskReviewAction.MarkReviewed,"UTC"));Assert.Empty(WorkspaceReminderCues.Read(snapshot.Document));Assert.Empty(snapshot.Document.Completions);Assert.All(snapshot.Commands,e=>Assert.Empty(e.Effects));
    }
}
