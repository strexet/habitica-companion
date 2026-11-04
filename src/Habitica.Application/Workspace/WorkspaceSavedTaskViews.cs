using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public static class WorkspaceSavedTaskViews
{
    public static WorkspaceTaskPage Evaluate(WorkspaceDocument document,Guid viewId,WorkspaceReadFilter page,DateTimeOffset? asOfUtc=null,DateOnly? expectedEvaluationDay=null)
    {
        if(viewId==Guid.Empty) throw new ArgumentException("invalid_view");
        if(WorkspaceRules.Validate(document).Count!=0) throw new InvalidOperationException("saved_views_unavailable");
        var view=document.SavedTaskViews?.SingleOrDefault(value=>value.Id==viewId&&!value.IsArchived)
            ?? throw new InvalidOperationException("saved_view_not_found");
        var evaluation=WorkspaceRelativeDates.Evaluate(view.Query,asOfUtc??default,expectedEvaluationDay);
        if(view.Query.HasRelativeDates&&page.Offset>0&&expectedEvaluationDay is null)throw new ArgumentException("evaluation_day_required");
        var f=view.Query.Filter;
        var filter=new WorkspaceTaskFilter(f.Query,f.Kind,f.State,f.Priority,f.ProjectId,f.CategoryId,f.LabelId,
            evaluation?.Planned?.From??f.PlannedFrom,evaluation?.Planned?.Through??f.PlannedTo,evaluation?.Deadline?.From??f.DeadlineFrom,evaluation?.Deadline?.Through??f.DeadlineTo,f.ProjectionState,page.Limit,page.Offset,page.ExpectedRevision,f.ExecutionStage);
        bool Eligible(CompanionTask task)=>WorkspaceRelativeDates.MatchesMissingDates(task,evaluation);
        var result=view.Query.Source==WorkspaceSavedTaskSource.UnblockedTodos
            ? WorkspaceUnblockedTodos.Evaluate(document,filter,Eligible) : WorkspaceTaskQuery.Evaluate(document,filter,Eligible);
        return result with{Evaluation=evaluation};
    }
}
