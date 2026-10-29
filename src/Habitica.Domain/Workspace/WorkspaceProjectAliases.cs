namespace Habitica.Domain.Workspace;

public static class WorkspaceProjectAliases
{
    public static string Normalize(string value) => value.Trim().ToLowerInvariant();

    public static bool IsValid(string value)
    {
        if(value.Length>256)return false;
        var trimmed=value.Trim();
        if(trimmed.Any(c=>!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-')))return false;
        var normalized=Normalize(value);
        return normalized.Length is >=1 and <=64 && normalized[0]!='-' && normalized[^1]!='-'
            && !normalized.Contains("--",StringComparison.Ordinal)
            && normalized.All(c=>c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }

    public static bool IsCanonical(string value) => IsValid(value) && value==Normalize(value);

    // Releasing a retained alias cannot amend or reopen other project metadata.
    public static bool IsRetainedClear(WorkspaceOrganizationCommand command) => command.Kind==WorkspaceOrganizationKind.Project
        && command.Operation==WorkspaceOrganizationOperation.Update && command.Patch==new WorkspaceOrganizationPatch(ClearAlias:true);
}
