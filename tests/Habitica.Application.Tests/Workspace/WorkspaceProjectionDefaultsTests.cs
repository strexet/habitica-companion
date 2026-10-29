using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Tests.Workspace;
public sealed class WorkspaceProjectionDefaultsTests
{
    [Fact]
    public void Portable_defaults_cover_all_kinds_native_metadata_and_retained_mapping_refusals()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","task-projection-defaults-v1.json")));
        foreach(var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var document=WorkspaceTaskApiJson.Deserialize<WorkspaceDocument>(row.GetProperty("document").GetRawText());
            var command=WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(row.GetProperty("command").GetRawText());
            var prepared=WorkspaceTodoProjectionPolicy.Prepare(document,command);
            Assert.True(row.GetProperty("projected").GetBoolean()==prepared.ProjectToHabitica,row.GetProperty("name").GetString());
            Assert.True(row.GetProperty("preparedFailure").GetString()==WorkspaceTodoProjectionPolicy.Rejection(document,prepared),row.GetProperty("name").GetString());
            Assert.Equal(row.GetProperty("rawFailure").GetString(),WorkspaceTodoProjectionPolicy.Rejection(document,command));
            Assert.False(command.ProjectToHabitica);
            if(command.Operation==WorkspaceOperation.CreateHabit){Assert.True(prepared.Patch!.HabiticaDetails!.Up);Assert.True(prepared.Patch.HabiticaDetails.Down);}
        }
    }
}
