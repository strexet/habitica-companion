using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTaskReviewTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-26T08:00:00Z");
    private static WorkspaceJournalSnapshot Initial()
    {
        var snapshot=new WorkspaceJournalSnapshot(new WorkspaceDocument{Id=Guid.NewGuid()},1,[],[]);
        return WorkspaceCommandEngine.Accept(snapshot,new(Guid.NewGuid(),snapshot.Document.Id,1,WorkspaceOperation.CreateTodo,Guid.NewGuid(),new(Text:"Voluntary review",Priority:CompanionPriority.High,Deadline:new DateOnly(2026,11,1))),Now).Snapshot!;
    }
    private static WorkspaceCommand Command(WorkspaceJournalSnapshot s,WorkspaceTaskReview patch)=>new(Guid.NewGuid(),s.Document.Id,s.Document.Revision,WorkspaceOperation.ReviewTask,s.Document.Tasks[0].Id,new(Review:patch));
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot s,WorkspaceTaskReview patch)
    {
        var result=WorkspaceCommandEngine.Accept(s,Command(s,patch),Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);Assert.Empty(result.Entry!.Effects);Assert.Equal(ProjectionCommandState.NotRequested,result.Entry.ProjectionState);Assert.True(WorkspaceJournalValidation.IsValid(result.Snapshot!));return result.Snapshot!;
    }
    [Fact]
    public void Shared_v10_identity_matches_all_six_actions_without_changing_prior_enum_values()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","task-review-command-v10.json")));
        foreach(var row in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            var command=WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(row.GetProperty("command").GetRawText());Assert.Equal(row.GetProperty("payloadHash").GetString(),WorkspaceCommandIdentity.Hash(command));
        }
        Assert.Equal(14,(int)WorkspaceOperation.SetTodoStage);Assert.Equal(15,(int)WorkspaceOperation.EnsureProjection);Assert.Equal(16,(int)WorkspaceOperation.ReviewTask);
    }
    [Fact]
    public void Set_snooze_clear_and_mark_reviewed_have_distinct_causal_history_and_preserve_native_and_game_fields()
    {
        foreach(var mapping in Enum.GetValues<ProjectionState>())
        {
            var s=Initial();s=s with{Document=s.Document with{Projections=[new(Guid.NewGuid(),s.Document.Revision,s.Document.Tasks[0].Id,mapping,mapping==ProjectionState.Unlinked?null:Guid.NewGuid().ToString())]}};var original=s.Document.Tasks[0];var before=s.Document;
            s=Apply(s,new(TaskReviewAction.Set,"Asia/Novosibirsk",Now.AddDays(-1)));s=Apply(s,new(TaskReviewAction.Snooze,"Asia/Novosibirsk",Now.AddDays(1)));
            Assert.Equal(Now.AddDays(1),s.Document.Tasks[0].ReviewAfterUtc);Assert.DoesNotContain(s.Document.ReviewEvents!,r=>r.Action==TaskReviewAction.MarkReviewed);
            s=Apply(s,new(TaskReviewAction.SetStale,"UTC",StaleSinceUtc:Now.AddDays(-3)));s=Apply(s,new(TaskReviewAction.MarkReviewed,"UTC"));Assert.Null(s.Document.Tasks[0].ReviewAfterUtc);Assert.Equal(Now.AddDays(-3),s.Document.Tasks[0].StaleSinceUtc);Assert.Single(s.Document.ReviewEvents!,r=>r.Action==TaskReviewAction.MarkReviewed);
            s=Apply(s,new(TaskReviewAction.Set,"UTC",Now.AddDays(2)));s=Apply(s,new(TaskReviewAction.Clear,"UTC"));s=Apply(s,new(TaskReviewAction.ClearStale,"UTC"));
            Assert.Equal(original with{Revision=s.Document.Revision,UpdatedAtUtc=Now},s.Document.Tasks[0]);Assert.Equal(before.Completions,s.Document.Completions);Assert.Equal(before.HabitScores,s.Document.HabitScores);Assert.Equal(before.Projections,s.Document.Projections);Assert.Equal(7,s.Document.ReviewEvents!.Count);
            Assert.Equal(WorkspaceJournalJson.Serialize(s),WorkspaceJournalJson.Serialize(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(s))));
        }
    }
    [Fact]
    public void Original_command_is_idempotent_and_stale_or_changed_intents_do_not_add_history()
    {
        var s=Initial();var command=Command(s,new(TaskReviewAction.MarkReviewed,"UTC"));var result=WorkspaceCommandEngine.Accept(s,command,Now);s=result.Snapshot!;
        Assert.Equal(SubmissionDisposition.Duplicate,WorkspaceCommandEngine.Accept(s,command,Now).Disposition);Assert.Single(s.Document.ReviewEvents!);
        Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceCommandEngine.Accept(s,command with{Patch=new(Review:new(TaskReviewAction.Clear,"UTC"))},Now).Disposition);
        Assert.Equal(SubmissionDisposition.RevisionConflict,WorkspaceCommandEngine.Accept(s,command with{Id=Guid.NewGuid()},Now).Disposition);
    }
    [Fact]
    public void Malformed_mixed_projected_missing_and_invalid_snooze_intents_fail()
    {
        var s=Initial();var command=Command(s,new(TaskReviewAction.Set,"UTC",Now.AddDays(1)));
        foreach(var invalid in new[]{command with{Patch=new(Review:new(TaskReviewAction.Set,"UTC"))},command with{Patch=new(Review:new((TaskReviewAction)99,"UTC"))},command with{Patch=new(Review:new(TaskReviewAction.Set,"Missing/Zone",Now))},command with{Patch=new(Review:new(TaskReviewAction.Clear,"UTC",Now))},command with{Patch=new(Review:new(TaskReviewAction.Snooze,"UTC",Now.AddDays(1)))},command with{Patch=new(Text:"Mixed",Review:command.Patch!.Review)},command with{ProjectToHabitica=true},command with{TaskId=Guid.NewGuid()},command with{Operation=WorkspaceOperation.UpdateTask},command with{Patch=new(Review:new(TaskReviewAction.SetStale,"UTC",StaleSinceUtc:Now.AddDays(1)))}})
            Assert.Equal(SubmissionDisposition.Invalid,WorkspaceCommandEngine.Accept(s,invalid,Now).Disposition);
        s=Apply(s,command.Patch!.Review!);Assert.Equal(SubmissionDisposition.Invalid,WorkspaceCommandEngine.Accept(s,Command(s,new(TaskReviewAction.Snooze,"UTC",Now)),Now).Disposition);
    }
    [Fact]
    public void Legacy_fields_without_review_events_remain_valid_and_history_paging_does_not_change_task_state()
    {
        var s=Initial();s=s with{Document=s.Document with{Tasks=[s.Document.Tasks[0] with{ReviewAfterUtc=Now.AddDays(-1),StaleSinceUtc=Now.AddDays(-2)}]}};
        var old=WorkspaceJson.Serialize(s.Document);Assert.DoesNotContain("reviewEvents",old);Assert.Equal(s.Document.Tasks[0],WorkspaceJson.Deserialize(old).Tasks[0]);
        s=Apply(s,new(TaskReviewAction.Snooze,"UTC",Now.AddDays(1)));s=Apply(s,new(TaskReviewAction.MarkReviewed,"UTC"));var page=WorkspaceTaskReviews.Read(s.Document,s.Document.Tasks[0].Id,1);
        Assert.Equal(2,page.MatchedCount);Assert.Equal(TaskReviewAction.MarkReviewed,page.Items[0].Action);Assert.True(page.Truncated);Assert.Throws<ArgumentException>(()=>WorkspaceTaskReviews.Read(s.Document,s.Document.Tasks[0].Id,1,1));Assert.Throws<InvalidOperationException>(()=>WorkspaceTaskReviews.Read(s.Document,s.Document.Tasks[0].Id,1,1,1));
        Assert.Equal(TaskReviewAction.Snooze,WorkspaceTaskReviews.Read(s.Document,s.Document.Tasks[0].Id,1,1,s.Document.Revision).Items[0].Action);Assert.Equal(CompanionTaskState.Active,s.Document.Tasks[0].State);
    }
    [Fact]
    public void Forged_cue_history_or_scoring_receipt_is_rejected()
    {
        var s=Apply(Initial(),new(TaskReviewAction.Set,"UTC",Now.AddDays(1)));var review=s.Document.ReviewEvents![0];var entry=s.Commands[^1];
        Assert.NotEmpty(WorkspaceRules.Validate(s.Document with{ReviewEvents=[review with{TaskId=Guid.NewGuid()}]}));Assert.NotEmpty(WorkspaceRules.Validate(s.Document with{ReviewEvents=[review,review with{Id=Guid.NewGuid()}]}));Assert.NotEmpty(WorkspaceRules.Validate(s.Document with{Tasks=[s.Document.Tasks[0] with{ReviewAfterUtc=null}]}));
        Assert.False(WorkspaceJournalValidation.IsValid(s with{Document=s.Document with{ReviewEvents=[]}}));
        Assert.False(WorkspaceJournalValidation.IsValid(s with{Commands=s.Commands.SkipLast(1).Append(entry with{Effects=[ProjectionEffectKind.ScoreUp],ProjectionState=ProjectionCommandState.Pending}).ToArray()}));
    }
}
