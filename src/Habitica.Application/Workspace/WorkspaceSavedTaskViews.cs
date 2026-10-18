using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public static class WorkspaceSavedTaskViews
{
    public static WorkspaceTaskPage Evaluate(WorkspaceDocument document,Guid viewId,WorkspaceReadFilter page)
    {
        if(viewId==Guid.Empty) throw new ArgumentException("invalid_view");
        if(WorkspaceRules.Validate(document).Count!=0) throw new InvalidOperationException("saved_views_unavailable");
        var view=document.SavedTaskViews?.SingleOrDefault(value=>value.Id==viewId&&!value.IsArchived)
            ?? throw new InvalidOperationException("saved_view_not_found");
        var f=view.Query.Filter;
        var filter=new WorkspaceTaskFilter(f.Query,f.Kind,f.State,f.Priority,f.ProjectId,f.CategoryId,f.LabelId,
            f.PlannedFrom,f.PlannedTo,f.DeadlineFrom,f.DeadlineTo,f.ProjectionState,page.Limit,page.Offset,page.ExpectedRevision,f.ExecutionStage);
        return view.Query.Source==WorkspaceSavedTaskSource.UnblockedTodos
            ? WorkspaceUnblockedTodos.Evaluate(document,filter) : WorkspaceTaskQuery.Evaluate(document,filter);
    }
}
