using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public enum WorkspaceOrganizationKind { Category, Project, Section, Label, Goal, SavedTaskView }
public enum WorkspaceOrganizationOperation { Create, Update, Archive }
public sealed record WorkspaceOrganizationPatch(string? Name = null, Guid? CategoryId = null, bool ClearCategory = false,
    Guid? ProjectId = null, int? Position = null, IReadOnlyList<Guid>? TaskIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkspaceSavedTaskQuery? Query = null);
public sealed record WorkspaceOrganizationCommand(Guid Id, Guid WorkspaceId, long ExpectedRevision,
    [property: JsonRequired] WorkspaceOrganizationKind Kind, [property: JsonRequired] WorkspaceOrganizationOperation Operation,
    Guid EntityId, WorkspaceOrganizationPatch? Patch = null);
public sealed record WorkspaceOrganizationSnapshot([property: JsonRequired] WorkspaceOrganizationKind Kind, Guid Id, long Revision,
    [property: JsonRequired] string Name, bool IsArchived = false, Guid? CategoryId = null, Guid? ProjectId = null, int Position = 0, IReadOnlyList<Guid>? TaskIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkspaceSavedTaskQuery? Query = null)
{
    public static WorkspaceOrganizationSnapshot? From(WorkspaceDocument document, WorkspaceOrganizationKind kind, Guid id) => kind switch
    {
        WorkspaceOrganizationKind.Category => document.Categories.FirstOrDefault(row=>row.Id==id) is { } row ? new(kind,row.Id,row.Revision,row.Name,row.IsArchived) : null,
        WorkspaceOrganizationKind.Project => document.Projects.FirstOrDefault(row=>row.Id==id) is { } row ? new(kind,row.Id,row.Revision,row.Name,row.IsArchived,CategoryId:row.CategoryId) : null,
        WorkspaceOrganizationKind.Section => document.Sections.FirstOrDefault(row=>row.Id==id) is { } row ? new(kind,row.Id,row.Revision,row.Name,row.IsArchived,ProjectId:row.ProjectId,Position:row.Position) : null,
        WorkspaceOrganizationKind.Label => document.Labels.FirstOrDefault(row=>row.Id==id) is { } row ? new(kind,row.Id,row.Revision,row.Name,row.IsArchived) : null,
        WorkspaceOrganizationKind.Goal => document.Goals.FirstOrDefault(row=>row.Id==id) is { } row ? new(kind,row.Id,row.Revision,row.Name,row.IsArchived,TaskIds:row.TaskIds) : null,
        WorkspaceOrganizationKind.SavedTaskView => document.SavedTaskViews?.FirstOrDefault(row=>row.Id==id) is {} row ? new(kind,row.Id,row.Revision,row.Name,row.IsArchived,Query:row.Query) : null,
        _ => null
    };
    public bool Equivalent(WorkspaceOrganizationSnapshot? other) => other is not null && Kind==other.Kind && Id==other.Id && Revision==other.Revision
        && Query==other.Query && Name==other.Name && IsArchived==other.IsArchived && CategoryId==other.CategoryId && ProjectId==other.ProjectId && Position==other.Position
        && (TaskIds is null ? other.TaskIds is null : other.TaskIds is not null && TaskIds.SequenceEqual(other.TaskIds));
}
public sealed record WorkspaceOrganizationEntry([property: JsonRequired] WorkspaceOrganizationCommand Command,
    [property: JsonRequired] string PayloadHash, DateTimeOffset AcceptedAtUtc, [property: JsonRequired] WorkspaceOrganizationSnapshot Snapshot);

public static class WorkspaceOrganizationCommands
{
    public static bool IsValid(WorkspaceOrganizationCommand command)
    {
        if (command.Id==Guid.Empty || command.WorkspaceId==Guid.Empty || command.EntityId==Guid.Empty || command.ExpectedRevision is <1 or >=WorkspaceDocument.MaxRevision
            || !Enum.IsDefined(command.Kind) || !Enum.IsDefined(command.Operation)) return false;
        var patch=command.Patch;
        if (command.Operation==WorkspaceOrganizationOperation.Archive) return patch is null;
        if (patch is null || patch==new WorkspaceOrganizationPatch() || patch.Name is not null && (string.IsNullOrWhiteSpace(patch.Name)||patch.Name.Length>256)
            || command.Operation==WorkspaceOrganizationOperation.Create && patch.Name is null
            || patch.CategoryId==Guid.Empty || patch.ProjectId==Guid.Empty || patch.Position<0 || patch.ClearCategory&&patch.CategoryId is not null
            || patch.TaskIds is not null && (patch.TaskIds.Count>100||patch.TaskIds.Any(id=>id==Guid.Empty)||patch.TaskIds.Distinct().Count()!=patch.TaskIds.Count)) return false;
        if (command.Kind!=WorkspaceOrganizationKind.Project && (patch.CategoryId is not null||patch.ClearCategory)
            || command.Kind!=WorkspaceOrganizationKind.Section && (patch.ProjectId is not null||patch.Position is not null)
            || command.Kind!=WorkspaceOrganizationKind.Goal && patch.TaskIds is not null
            || command.Kind!=WorkspaceOrganizationKind.SavedTaskView && patch.Query is not null
            || patch.Query is not null && !patch.Query.IsValid()
            || command.Kind==WorkspaceOrganizationKind.SavedTaskView && command.Operation==WorkspaceOrganizationOperation.Create && patch.Query is null) return false;
        if (command.Operation==WorkspaceOrganizationOperation.Create && patch.ClearCategory) return false;
        if (command.Kind==WorkspaceOrganizationKind.Section && (command.Operation==WorkspaceOrganizationOperation.Create ? patch.ProjectId is null : patch.ProjectId is not null)) return false;
        return true;
    }

    public static WorkspaceOrganizationSnapshot Apply(WorkspaceOrganizationCommand command, WorkspaceOrganizationSnapshot? current)
    {
        var patch=command.Patch;var revision=command.ExpectedRevision+1;
        if (command.Operation==WorkspaceOrganizationOperation.Create)
            return new(command.Kind,command.EntityId,revision,patch!.Name!,CategoryId:patch.CategoryId,ProjectId:patch.ProjectId,Position:patch.Position??0,
                TaskIds:command.Kind==WorkspaceOrganizationKind.Goal?patch.TaskIds??Array.Empty<Guid>():null,Query:patch.Query);
        if (command.Operation==WorkspaceOrganizationOperation.Archive) return current! with { Revision=revision,IsArchived=true };
        return current! with { Revision=revision,Name=patch!.Name??current.Name,CategoryId=patch.ClearCategory?null:patch.CategoryId??current.CategoryId,
            Position=patch.Position??current.Position,TaskIds=patch.TaskIds??current.TaskIds,Query=patch.Query??current.Query };
    }

    public static string Hash(WorkspaceOrganizationCommand command)
    {
        static string Name<T>(T value) where T:struct,Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
        var patch=command.Patch;var fields=new List<string?> { command.Id.ToString("D"),command.WorkspaceId.ToString("D"),command.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
            Name(command.Kind),Name(command.Operation),command.EntityId.ToString("D"),patch is null?"0":"1",patch?.Name,patch?.CategoryId?.ToString("D"),patch?.ClearCategory==true?"1":"0",
            patch?.ProjectId?.ToString("D"),patch?.Position?.ToString(CultureInfo.InvariantCulture),patch?.TaskIds?.Count.ToString(CultureInfo.InvariantCulture) };
        foreach(var id in patch?.TaskIds??Array.Empty<Guid>())fields.Add(id.ToString("D"));
        var extended=command.Kind==WorkspaceOrganizationKind.SavedTaskView || patch?.Query is not null;
        if(extended)
        {
            var query=patch?.Query;fields.Add(query is null?"0":"1");
            if(query is not null)
            {
                var f=query.Filter;fields.AddRange([Name(query.Source),f.Query,f.Kind is {} kind?Name(kind):null,f.State is {} state?Name(state):null,
                    f.Priority is {} priority?Name(priority):null,f.ProjectId?.ToString("D"),f.CategoryId?.ToString("D"),f.LabelId?.ToString("D"),
                    f.PlannedFrom?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),f.PlannedTo?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
                    f.DeadlineFrom?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),f.DeadlineTo?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
                    f.ProjectionState is {} projection?Name(projection):null,f.ExecutionStage is {} stage?Name(stage):null]);
            }
        }
        var body=(extended?"companion-organization-command-v2\n":"companion-organization-command-v1\n")+string.Concat(fields.Select(value=>value is null?"-1:":Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture)+":"+value));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }
}
