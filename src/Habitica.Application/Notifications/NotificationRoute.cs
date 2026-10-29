namespace Habitica.Application.Notifications;

public sealed record NotificationTarget(Guid WorkspaceId,Guid TaskId,Guid OccurrenceId);
public static class NotificationRoute
{
    private const string Path="/todo/reminders/open";
    public static string Build(NotificationTarget target)
    {
        if(target.WorkspaceId==Guid.Empty||target.TaskId==Guid.Empty||target.OccurrenceId==Guid.Empty)throw new ArgumentException("Notification identities must be nonempty.");
        return $"{Path}?workspaceId={target.WorkspaceId:D}&taskId={target.TaskId:D}&occurrenceId={target.OccurrenceId:D}";
    }
    public static bool TryParse(string? route,out NotificationTarget? target)
    {
        target=null;if(route is null||route.Length>300||!route.StartsWith(Path+"?",StringComparison.Ordinal)||route.Contains('#'))return false;
        var values=Query(route[(Path.Length+1)..]);
        if(values is null||values.Count!=3||!values.TryGetValue("workspaceId",out var workspace)||!values.TryGetValue("taskId",out var task)||!values.TryGetValue("occurrenceId",out var occurrence)
            ||!Guid.TryParseExact(workspace,"D",out var w)||!Guid.TryParseExact(task,"D",out var t)||!Guid.TryParseExact(occurrence,"D",out var o)||w==Guid.Empty||t==Guid.Empty||o==Guid.Empty)return false;
        target=new(w,t,o);return true;
    }
    private static Dictionary<string,string>? Query(string query)
    {
        var values=new Dictionary<string,string>(StringComparer.Ordinal);
        try{foreach(var pair in query.Split('&')){var parts=pair.Split('=');if(parts.Length!=2||!values.TryAdd(Uri.UnescapeDataString(parts[0]),Uri.UnescapeDataString(parts[1])))return null;}}
        catch(UriFormatException){return null;}return values;
    }
    public static string SignInDestination(string absoluteUri)
    {
        if(!Uri.TryCreate(absoluteUri,UriKind.Absolute,out var uri)||uri.Query.Length>1500)return "/dashboard";
        if(TryParse(uri.PathAndQuery,out var current))return Build(current!);
        var query=Query(uri.Query.TrimStart('?'));
        return query is not null&&query.TryGetValue("returnTo",out var route)&&TryParse(route,out var target)?Build(target!):"/dashboard";
    }
}
