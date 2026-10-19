namespace Habitica.WebApp.Tests.Workspace;

internal static class WorkspaceLayoutEvidence
{
    public static void Save(string name, string markup, [System.Runtime.CompilerServices.CallerMemberName] string interactionTest = "")
    {
        var state = name.Contains("unavailable") ? "error" : name.Contains("fresh") ? "unknown-access" : name;
        var route = name.StartsWith("canonical-") ? "/tasks" : "/settings";
        var surfaces = new List<string> { "form", "actions", "disclosure" };
        if (name.Contains("preview") || name.Contains("archive")) surfaces.Add("confirmation");
        if (name.Contains("disabled") || name.Contains("fresh")) surfaces.Add("disabled-controls");
        if (name.Contains("unavailable")) surfaces.Add("error");
        Habitica.WebApp.Tests.Qualification.PageLayoutEvidence.Save(name, markup, route, state, surfaces.ToArray(), interactionTest);
    }
}
