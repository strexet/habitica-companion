using Habitica.Domain.Workspace;
using Habitica.Application.Workspace;
using System.Text.Json;
namespace Habitica.Application.Tests.Workspace;
public sealed class LinkedDailySchedulePolicyTests
{
    private static HabiticaDailySchedule Before=>new("daily",1,new(2026,10,27,0,0,0,TimeSpan.Zero),new Dictionary<string,bool>{{"su",true},{"m",true},{"t",true},{"w",true},{"th",true},{"f",true},{"s",true}},[],[]);
    [Fact]public void A_reviewed_supported_linked_weekly_edit_preserves_complete_before_and_expected_after_fields()
    {
        var before=Before;var after=new RecurrenceRule(RecurrenceFrequency.Weekly,1,new(2026,11,2),"UTC",[DayOfWeek.Monday,DayOfWeek.Wednesday],[]);var review=LinkedDailySchedulePolicy.Review(before,after,new(0,0));Assert.True(review.Supported,review.Failure);Assert.Same(before,review.Before);Assert.Equal("daily",review.BeforeNative!.Frequency.ToString().ToLowerInvariant());Assert.Equal("weekly",review.ExpectedAfter!.Frequency);Assert.True(review.ExpectedAfter.Repeat["m"]);Assert.True(review.ExpectedAfter.Repeat["w"]);Assert.False(review.ExpectedAfter.Repeat["f"]);Assert.Empty(review.ExpectedAfter.WeeksOfMonth);
    }
    [Fact]public void Independent_schedule_examples_refuse_field_loss_and_preserve_provider_defaults()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","linked-daily-schedule-v1.json")));
        foreach(var example in fixture.RootElement.GetProperty("examples").EnumerateArray())
        {
            var before=WorkspaceTaskApiJson.Deserialize<HabiticaDailySchedule>(example.GetProperty("before").GetRawText());
            var proposed=WorkspaceTaskApiJson.Deserialize<RecurrenceRule>(example.GetProperty("proposed").GetRawText());
            var account=WorkspaceTaskApiJson.Deserialize<DailyAccountSchedule>(example.GetProperty("account").GetRawText());
            var review=LinkedDailySchedulePolicy.Review(before,proposed,account);
            Assert.Equal(example.GetProperty("failure").GetString(),review.Failure);
            if(review.Supported){var expected=WorkspaceTaskApiJson.Deserialize<HabiticaDailySchedule>(example.GetProperty("expectedAfter").GetRawText());Assert.Equal(expected.Frequency,review.ExpectedAfter!.Frequency);Assert.Equal(expected.EveryX,review.ExpectedAfter.EveryX);Assert.Equal(expected.StartDate,review.ExpectedAfter.StartDate);Assert.Equal(expected.Repeat,review.ExpectedAfter.Repeat);Assert.Equal(expected.DaysOfMonth,review.ExpectedAfter.DaysOfMonth);Assert.Equal(expected.WeeksOfMonth,review.ExpectedAfter.WeeksOfMonth);}
        }
    }
    [Fact]public void Exact_linked_schedule_command_retains_an_effective_dated_native_epoch_and_original_receipt()
    {
        var now=DateTimeOffset.Parse("2026-11-02T13:00:00Z");var state=new WorkspaceJournalSnapshot(new WorkspaceDocument{Id=Guid.NewGuid()},1,[]);
        var taskId=Guid.NewGuid();var created=WorkspaceCommandEngine.Accept(state,new(Guid.NewGuid(),state.Document.Id,1,WorkspaceOperation.CreateDaily,taskId,new(Text:"Schedule fixture")),now);Assert.Equal(SubmissionDisposition.Accepted,created.Disposition);state=created.Snapshot!;
        var remoteId=Guid.NewGuid();var fingerprint=new string('a',64);var revision=state.Document.Revision+1;
        state=state with {Document=state.Document with {Revision=revision,Projections=[new(Guid.NewGuid(),revision,taskId,ProjectionState.Linked,remoteId.ToString("D"),Fingerprint:fingerprint)]},StoreVersion=state.StoreVersion+1};
        var desired=new RecurrenceRule(RecurrenceFrequency.Daily,3,new(2026,11,2),"UTC",[],[]);var review=LinkedDailySchedulePolicy.Review(Before,desired,new(0,0));
        var context=new LinkedDailyScheduleContext(remoteId,fingerprint,now,new(0,0),Before,review.ExpectedAfter!);
        var command=new WorkspaceCommand(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.EditLinkedDailySchedule,taskId,new(Recurrence:desired,LinkedSchedule:context),ProjectToHabitica:true);
        var accepted=WorkspaceCommandEngine.Accept(state,command,now);Assert.Equal(SubmissionDisposition.Accepted,accepted.Disposition);Assert.Equal(new[]{ProjectionEffectKind.Update},accepted.Entry!.Effects);Assert.Equal(desired,accepted.Entry.TaskSnapshot!.Recurrence);Assert.Equal(now,accepted.Entry.AcceptedAtUtc);
        var archive=WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(accepted.Snapshot!));Assert.Equal(context.Fingerprint,archive.Commands.Last().Command.Patch!.LinkedSchedule!.Fingerprint);
        var duplicate=WorkspaceCommandEngine.Accept(archive,command,now.AddMinutes(10));Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Equal(accepted.Entry.PayloadHash,duplicate.Entry!.PayloadHash);
        Assert.Equal("linked_schedule_review_required",WorkspaceCommandEngine.Accept(state,command,now.AddSeconds(91)).ErrorCode);
        Assert.Equal("unsupported_projection",WorkspaceCommandEngine.Accept(state,command with {Operation=WorkspaceOperation.UpdateTask,Patch=new(Recurrence:desired)},now).ErrorCode);
        Assert.Empty(accepted.Snapshot!.Document.Completions);Assert.Empty(accepted.Snapshot.Document.HabitScores);
    }
    [Fact]public void Portable_schedule_command_binds_exact_context_and_preserves_legacy_hash_versions()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","linked-daily-command-v12.json")));
        var command=WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(fixture.RootElement.GetProperty("command").GetRawText());Assert.Equal(fixture.RootElement.GetProperty("payloadHash").GetString(),WorkspaceCommandIdentity.Hash(command));
        Assert.NotEqual(WorkspaceCommandIdentity.Hash(command),WorkspaceCommandIdentity.Hash(command with{Patch=command.Patch! with{LinkedSchedule=command.Patch.LinkedSchedule! with{Account=new(-420,0)}}}));
    }
}
