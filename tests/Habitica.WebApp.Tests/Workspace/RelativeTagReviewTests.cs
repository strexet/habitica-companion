using Habitica.Application.Workspace;using Habitica.Domain.Workspace;
namespace Habitica.WebApp.Tests.Workspace;
public sealed class RelativeTagReviewTests
{
    [Fact]public void Native_relative_group_selection_requires_exact_explicit_clock_and_preserves_automatic_fence()
    {
        var id=Guid.NewGuid();var selected=Guid.NewGuid();var at=DateTimeOffset.Parse("2026-11-03T00:30:00Z");
        var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Tasks=[new(selected,1,"No planned date",CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,at,at)],SavedTaskViews=[new(id,1,"Relative",new(WorkspaceSavedTaskSource.Tasks,new(PlannedRelative:WorkspaceRelativeDatePreset.NoDate),"America/New_York"))]};
        var fenced=WorkspaceTagCorrespondence.Desired(doc).Single();Assert.Equal("relative_dates_require_review",fenced.UnavailableReason);Assert.Empty(fenced.TaskIds);
        var reviewed=WorkspaceTagCorrespondence.Desired(doc,id,at).Single();Assert.Null(reviewed.UnavailableReason);Assert.Contains(selected,reviewed.TaskIds);Assert.Equal(new DateOnly(2026,11,2),reviewed.Evaluation!.LocalDate);Assert.Equal("America/New_York",reviewed.Evaluation.TimeZoneId);
        Assert.Throws<InvalidOperationException>(()=>WorkspaceTagCorrespondence.Desired(doc,id));
    }
}
