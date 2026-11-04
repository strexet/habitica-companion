using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Tests.Workspace;
public sealed class RelativeSavedTaskViewTests
{
    [Fact]public void A_saved_relative_definition_retains_intent_and_explicit_timezone_in_portable_storage()
    {
        const string text="""{"source":"tasks","filter":{"plannedRelative":"today"},"timeZoneId":"America/New_York"}""";
        var query=WorkspaceTaskApiJson.Deserialize<WorkspaceSavedTaskQuery>(text);Assert.True(query.IsValid());var retained=WorkspaceTaskApiJson.Serialize(query);Assert.Contains("\"plannedRelative\":\"today\"",retained);Assert.Contains("\"timeZoneId\":\"America/New_York\"",retained);
    }

    [Fact]public void Independent_clock_zone_calendar_examples_match_exact_bounds_and_retain_definitions()
    {
        using var data=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","relative-saved-dates-v1.json")));Assert.Equal(16,data.RootElement.GetProperty("cases").GetArrayLength());
        foreach(var row in data.RootElement.GetProperty("cases").EnumerateArray()){
            var query=WorkspaceTaskApiJson.Deserialize<WorkspaceSavedTaskQuery>(row.GetProperty("query").GetRawText());var before=WorkspaceTaskApiJson.Serialize(query);var expected=WorkspaceTaskApiJson.Deserialize<WorkspaceRelativeDateEvaluation>(row.GetProperty("evaluation").GetRawText());var actual=WorkspaceRelativeDates.Evaluate(query,row.GetProperty("asOfUtc").GetDateTimeOffset());Assert.Equal(expected,actual);Assert.Equal(before,WorkspaceTaskApiJson.Serialize(query));
        }
    }
    [Fact]public void Relative_selection_pages_use_one_explicit_day_and_never_filter_away_dependency_sources_or_mutate_tasks()
    {
        var now=DateTimeOffset.Parse("2026-11-03T00:30:00Z");var created=now.AddDays(-2);CompanionTask Task(string name,DateOnly? planned,DateOnly? deadline)=>new(Guid.NewGuid(),1,name,CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,created,created,PlannedDate:planned,Deadline:deadline);
        var before=Task("Before",new(2026,11,1),new(2026,11,1));var today=Task("Today",new(2026,11,2),new(2026,11,2));var after=Task("After",new(2026,11,3),new(2026,11,3));var missing=Task("Missing",null,null);var view=new WorkspaceSavedTaskView(Guid.NewGuid(),1,"Relative",new(WorkspaceSavedTaskSource.Tasks,new(PlannedRelative:WorkspaceRelativeDatePreset.Today),"America/New_York"));var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[before,today,after,missing],SavedTaskViews=[view]};var original=WorkspaceJson.Serialize(doc);
        Assert.Equal(today.Id,Assert.Single(WorkspaceSavedTaskViews.Evaluate(doc,view.Id,new(),now).Tasks).Id);var page=WorkspaceSavedTaskViews.Evaluate(doc with{Tasks=[today,today with{Id=Guid.NewGuid()}]},view.Id,new(Limit:1),now);Assert.True(page.Truncated);Assert.NotNull(page.Evaluation);Assert.Throws<ArgumentException>(()=>WorkspaceSavedTaskViews.Evaluate(doc,view.Id,new(Offset:1,ExpectedRevision:10),now));Assert.Equal("stale_evaluation_day",Assert.Throws<InvalidOperationException>(()=>WorkspaceSavedTaskViews.Evaluate(doc,view.Id,new(Offset:1,ExpectedRevision:10),now.AddHours(6),new(2026,11,2))).Message);
        foreach(var (preset,ids) in new[]{(WorkspaceRelativeDatePreset.BeforeToday,new[]{before.Id}),(WorkspaceRelativeDatePreset.NextSevenDays,new[]{today.Id,after.Id}),(WorkspaceRelativeDatePreset.NoDate,new[]{missing.Id})}){var changed=doc with{SavedTaskViews=[view with{Query=view.Query with{Filter=new(PlannedRelative:preset)}}]};Assert.Equal(ids,WorkspaceSavedTaskViews.Evaluate(changed,view.Id,new(),now).Tasks.Select(t=>t.Id));}
        Assert.Equal(original,WorkspaceJson.Serialize(doc));Assert.Empty(doc.Completions);Assert.Empty(doc.HabitScores);
    }
    [Fact]public void Conflicting_absolute_filters_missing_context_unsupported_zones_and_overflow_fail_explicitly()
    {
        var query=new WorkspaceSavedTaskQuery(WorkspaceSavedTaskSource.Tasks,new(PlannedRelative:WorkspaceRelativeDatePreset.Today),"UTC");var now=DateTimeOffset.Parse("2026-11-03T00:00:00Z");Assert.False((query with{Filter=query.Filter with{PlannedFrom=new(2026,11,1)}}).IsValid());Assert.False((query with{TimeZoneId=null}).IsValid());Assert.False((query with{Filter=new()}).IsValid());Assert.Throws<InvalidOperationException>(()=>WorkspaceRelativeDates.Evaluate(query,default));Assert.Equal("relative_timezone_unavailable",Assert.Throws<InvalidOperationException>(()=>WorkspaceRelativeDates.Evaluate(query with{TimeZoneId="Missing/Zone"},now)).Message);Assert.Equal("relative_bounds_unavailable",Assert.Throws<InvalidOperationException>(()=>WorkspaceRelativeDates.Bounds(WorkspaceRelativeDatePreset.NextSevenDays,new(9999,12,30))).Message);Assert.Equal("relative_bounds_unavailable",Assert.Throws<InvalidOperationException>(()=>WorkspaceRelativeDates.Bounds(WorkspaceRelativeDatePreset.BeforeToday,new(1,1,1))).Message);
    }

    [Fact]public void Missing_date_unblocked_selection_preserves_prerequisites_outside_the_result_filter()
    {
        var now=DateTimeOffset.Parse("2026-11-03T12:00:00Z");var prerequisite=new CompanionTask(Guid.NewGuid(),1,"Completed prerequisite",CompanionTaskKind.Todo,CompanionTaskState.Completed,CompanionPriority.Normal,now.AddDays(-2),now.AddDays(-1),PlannedDate:new(2026,11,1));var target=prerequisite with{Id=Guid.NewGuid(),Text="Unblocked missing date",State=CompanionTaskState.Active,PlannedDate=null,DependsOn=[prerequisite.Id]};var view=new WorkspaceSavedTaskView(Guid.NewGuid(),1,"Missing unblocked",new(WorkspaceSavedTaskSource.UnblockedTodos,new(PlannedRelative:WorkspaceRelativeDatePreset.NoDate),"UTC"));var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[prerequisite,target],Completions=[new(Guid.NewGuid(),1,prerequisite.Id,Guid.NewGuid(),now.AddDays(-1))],SavedTaskViews=[view]};Assert.Equal(target.Id,Assert.Single(WorkspaceSavedTaskViews.Evaluate(doc,view.Id,new(),now).Tasks).Id);Assert.Equal(2,doc.Tasks.Count);
    }

    [Fact]public void Relative_organization_hash_binds_both_date_fields_and_timezone_without_changing_old_commands()
    {
        using var data=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","relative-saved-command-v5.json")));var command=WorkspaceTaskApiJson.Deserialize<WorkspaceOrganizationCommand>(data.RootElement.GetProperty("command").GetRawText());Assert.Equal(data.RootElement.GetProperty("payloadHash").GetString(),WorkspaceOrganizationCommands.Hash(command));var changed=command with{Patch=command.Patch! with{Query=command.Patch!.Query! with{TimeZoneId="UTC"}}};Assert.NotEqual(WorkspaceOrganizationCommands.Hash(command),WorkspaceOrganizationCommands.Hash(changed));var doc=new WorkspaceDocument{Id=command.WorkspaceId,Revision=10};var before=WorkspaceJson.Serialize(doc);var now=DateTimeOffset.Parse("2026-11-03T00:00:00Z");var accepted=WorkspaceOrganizationEngine.Accept(new(doc,10,[],[]),command,now);Assert.Equal(SubmissionDisposition.Accepted,accepted.Disposition);Assert.Equal(SubmissionDisposition.Duplicate,WorkspaceOrganizationEngine.Accept(accepted.Snapshot!,command,now.AddDays(1)).Disposition);Assert.Empty(accepted.Snapshot!.Document.Tasks);Assert.Empty(accepted.Snapshot.Commands);Assert.Equal(before,WorkspaceJson.Serialize(doc));var invalid=command with{Patch=command.Patch! with{Query=command.Patch.Query! with{TimeZoneId="Missing/Zone"}}};Assert.Equal("relative_timezone_unavailable",WorkspaceOrganizationEngine.Accept(new(doc,10,[],[]),invalid,now).ErrorCode);
    }

    [Fact]public void Relative_saved_views_never_broaden_or_confirm_tag_membership_without_a_day_bound_review()
    {
        var now=DateTimeOffset.Parse("2026-11-03T12:00:00Z");var task=new CompanionTask(Guid.NewGuid(),1,"Retained original",CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,now,now);var view=new WorkspaceSavedTaskView(Guid.NewGuid(),1,"Relative",new(WorkspaceSavedTaskSource.Tasks,new(PlannedRelative:WorkspaceRelativeDatePreset.Today),"UTC"));var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[task],SavedTaskViews=[view]};var before=WorkspaceJson.Serialize(doc);var group=Assert.Single(WorkspaceTagCorrespondence.Desired(doc));Assert.Equal("relative_dates_require_review",group.UnavailableReason);Assert.Empty(group.TaskIds);Assert.False(WorkspaceTagCorrespondence.Inspect(doc,group,null).Confirmed);var coverage=WorkspaceHabiticaCoveragePolicy.Audit(doc);Assert.False(coverage.HasCompleteStoredCoverage);Assert.Equal(HabiticaCoverageState.NeedsReview,coverage.RequiredTags[0].State);Assert.Equal("relative_dates_require_review",coverage.RequiredTags[0].Reason);Assert.Equal(before,WorkspaceJson.Serialize(doc));
    }
}
