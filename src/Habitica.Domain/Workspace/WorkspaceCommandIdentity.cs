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
        var body = new StringBuilder("companion-command-v1\n");
        foreach (var field in fields) body.Append(field is null ? "-1:" : Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture) + ":" + field);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.ToString()))).ToLowerInvariant();
    }

}
