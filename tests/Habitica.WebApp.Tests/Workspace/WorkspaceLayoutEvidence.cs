namespace Habitica.WebApp.Tests.Workspace;

internal static class WorkspaceLayoutEvidence
{
    public static void Save(string name, string markup)
    {
        var directory = Environment.GetEnvironmentVariable("HABITICA_BROWSER_EVIDENCE");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name + ".html"), "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Synthetic " + name + " layout</title><link rel=\"stylesheet\" href=\"/mud.css\"><link rel=\"stylesheet\" href=\"/app.css\"></head><body><main style=\"padding:16px;max-width:1100px;margin:auto\"><p>Synthetic layout fixture rendered by the real Razor component. Interactions are qualified in component tests.</p>" + markup + "</main></body></html>");
    }
}
