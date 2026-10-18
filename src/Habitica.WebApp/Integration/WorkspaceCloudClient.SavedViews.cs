using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.WebApp.Integration;
public sealed partial class WorkspaceCloudClient : IWorkspaceSavedTaskViewApi
{
    public Task<WorkspaceTaskResponse<WorkspaceSavedViewList>> ListSavedTaskViewsAsync(WorkspaceReadFilter page,CancellationToken cancellationToken)
    {
        if(!ValidSavedPage(page))return Task.FromResult(new WorkspaceTaskResponse<WorkspaceSavedViewList>(false,null,"Invalid saved-view page."));
        return ReadSavedAsync<WorkspaceSavedViewList>("list_saved_task_views",SavedPageArgs(page),value=>value.Items is {} rows
            &&ValidSavedResult(page,value.Provenance,value.WorkspaceId,value.Revision,rows.Count,value.MatchedCount,value.Truncated,value.Offset,value.NextOffset)
            &&rows.All(row=>row is not null&&row.Id!=Guid.Empty&&row.Revision>=1&&row.Revision<=value.Revision&&!row.IsArchived
                &&!string.IsNullOrWhiteSpace(row.Name)&&row.Name.Length<=256&&row.Query is not null&&row.Query.IsValid())
            &&rows.Select(row=>row.Id).Distinct().Count()==rows.Count,cancellationToken);
    }
    public Task<WorkspaceTaskResponse<WorkspaceSavedViewTaskPage>> QuerySavedTaskViewAsync(Guid viewId,WorkspaceReadFilter page,CancellationToken cancellationToken)
    {
        if(viewId==Guid.Empty||!ValidSavedPage(page))return Task.FromResult(new WorkspaceTaskResponse<WorkspaceSavedViewTaskPage>(false,null,"Invalid saved-view identity or page."));
        var args=SavedPageArgs(page);args["viewId"]=viewId;
        return ReadSavedAsync<WorkspaceSavedViewTaskPage>("query_saved_task_view",args,value=>value.ViewId==viewId&&value.Tasks is {} rows
            &&ValidSavedResult(page,value.Provenance,value.WorkspaceId,value.Revision,rows.Count,value.MatchedCount,value.Truncated,value.Offset,value.NextOffset)
            &&rows.All(row=>row is not null&&row.Id!=Guid.Empty&&row.Revision>=1&&row.Revision<=value.Revision&&!row.IsArchived
                &&Enum.IsDefined(row.Kind)&&Enum.IsDefined(row.State)&&Enum.IsDefined(row.Priority)
                &&(row.Kind==CompanionTaskKind.Todo||row.State is not (CompanionTaskState.Completed or CompanionTaskState.Cancelled))&&HabiticaTaskDetails.IsValid(row.HabiticaDetails)&&!string.IsNullOrWhiteSpace(row.Text)&&row.Text.Length<=2048
                &&row.Notes?.Length is not >10000&&row.CreatedAtUtc!=default&&row.CreatedAtUtc.Offset==TimeSpan.Zero&&row.UpdatedAtUtc.Offset==TimeSpan.Zero&&row.CreatedAtUtc<=row.UpdatedAtUtc
                &&(row.ExecutionStage is null||row.Kind==CompanionTaskKind.Todo&&Enum.IsDefined(row.ExecutionStage.Value)))
            &&rows.Select(row=>row.Id).Distinct().Count()==rows.Count,cancellationToken);
    }
    private static bool ValidSavedPage(WorkspaceReadFilter page)=>page.Limit is >=1 and <=100&&page.Offset is >=0 and <=100000
        &&(page.ExpectedRevision is null||page.ExpectedRevision is >=1 and <=WorkspaceDocument.MaxRevision)&&(page.Offset==0||page.ExpectedRevision is not null);
    private static Dictionary<string,object?> SavedPageArgs(WorkspaceReadFilter page)
    {
        var args=new Dictionary<string,object?> { ["limit"]=page.Limit,["offset"]=page.Offset };if(page.ExpectedRevision is {} revision)args["expectedRevision"]=revision;return args;
    }
    private bool ValidSavedResult(WorkspaceReadFilter page,string provenance,Guid workspace,long revision,int? count,int matched,bool truncated,int offset,int? next)
    {
        if(provenance!="companion-canonical"||workspace!=WorkspaceId||revision is <1 or >WorkspaceDocument.MaxRevision||page.ExpectedRevision is {} expected&&revision!=expected
            ||count is null||count<0||count>page.Limit||matched is <0 or >100000||offset!=page.Offset||count!=Math.Min(page.Limit,Math.Max(0,matched-offset)))return false;
        var following=offset+count.Value;return truncated==(following<matched)&&next==(truncated?following:null);
    }
    private async Task<WorkspaceTaskResponse<T>> ReadSavedAsync<T>(string tool,object args,Func<T,bool> valid,CancellationToken cancellationToken)
    {
        if(WorkspaceId is null)return new(false,default,"Connect the cloud workspace first.");var generation=_generation;
        try
        {
            using var request=Request(HttpMethod.Post,"task-api");request.Content=new StringContent(WorkspaceTaskApiJson.Serialize(new {tool,arguments=args}),System.Text.Encoding.UTF8,"application/json");
            using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
            if(!response.IsSuccessStatusCode)return new(false,default,"Saved-view data is unavailable. Check current access and restart stale pages.");
            var value=WorkspaceTaskApiJson.Deserialize<T>(await BoundedTextAsync(response.Content,cancellationToken));
            if(generation!=_generation||value is null||!valid(value))return new(false,default,"Saved-view data could not be validated for the current connection.");
            return new(true,value,"Current saved-view data received.");
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){throw;}
        catch{return new(false,default,"Saved-view data is unavailable. No new action was requested.");}
    }
}
