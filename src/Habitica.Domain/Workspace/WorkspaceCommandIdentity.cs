using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Habitica.Domain.Workspace;

public static class WorkspaceCommandIdentity
{
    public static string Hash(WorkspaceCommand command)
    {
        static string? Name<T>(T? value) where T : struct, Enum => value is null ? null : JsonNamingPolicy.CamelCase.ConvertName(value.Value.ToString());
        var patch = command.Patch;
        var fields = new string?[]
        {
            command.Id.ToString("D"), command.WorkspaceId.ToString("D"), command.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
            Name<WorkspaceOperation>(command.Operation), command.TaskId.ToString("D"), patch is null ? "0" : "1",
            patch?.Text, patch?.Notes, patch?.ClearNotes == true ? "1" : "0", Name(patch?.Priority),
            patch?.Deadline?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), patch?.ClearDeadline == true ? "1" : "0",
            Name(command.Direction), command.Count.ToString(CultureInfo.InvariantCulture), command.OccurrenceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            command.ProjectToHabitica ? "1" : "0"
        };
        var details = patch?.HabiticaDetails;
        var body = new StringBuilder(details is null ? "companion-command-v1\n" : "companion-command-v2\n");
        foreach (var field in fields) body.Append(field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field);
        if (details is not null)
        {
            var extra = new List<string?> { details.Difficulty?.ToString("R", CultureInfo.InvariantCulture), Name(details.Attribute), details.Checklist?.Count.ToString(CultureInfo.InvariantCulture) };
            foreach (var item in details.Checklist ?? Array.Empty<TaskChecklistItem>()) { extra.Add(item.Id.ToString("D")); extra.Add(item.Text); extra.Add(item.Completed ? "1" : "0"); }
            foreach (var field in extra) body.Append(field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.ToString()))).ToLowerInvariant();
    }

}
