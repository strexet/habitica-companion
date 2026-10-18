using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Domain.Tests.Workspace;

public sealed class WorkspaceOrganizationHistoryTests
{
    private static JsonDocument Fixture()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","organization-history-v1.json")));
    [Fact]
    public void Independent_native_hashes_bind_kind_operation_entity_and_exact_patch()
    {
        using var fixture=Fixture();
        foreach(var item in fixture.RootElement.GetProperty("hashes").EnumerateArray())
        {
            var command=JsonSerializer.Deserialize<WorkspaceOrganizationCommand>(item.GetProperty("command").GetRawText(),new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={ new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false) } })!;Assert.True(WorkspaceOrganizationCommands.IsValid(command));Assert.Equal(item.GetProperty("payloadHash").GetString(),WorkspaceOrganizationCommands.Hash(command));
        }
    }
    [Fact]
    public void Neutral_model_and_full_journal_checks_preserve_defaults_and_reject_corruption()
    {
        using var fixture=Fixture();
        foreach(var item in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var raw=item.GetProperty("document");var documentValid=item.GetProperty("documentValid").GetBoolean();
            if(!documentValid){Assert.Throws<JsonException>(()=>WorkspaceJson.Deserialize(raw.GetRawText()));continue;}
            var document=WorkspaceJson.Deserialize(raw.GetRawText());var snapshot=new WorkspaceJournalSnapshot(document,document.Revision,[],[]);var valid=item.GetProperty("valid").GetBoolean();Assert.Equal(valid,WorkspaceJournalValidation.IsValid(snapshot));
            if(!valid){Assert.Throws<JsonException>(()=>WorkspaceJournalJson.Serialize(snapshot));continue;}
            var json=WorkspaceJournalJson.Serialize(snapshot);Assert.Equal(json,WorkspaceJournalJson.Serialize(WorkspaceJournalJson.Deserialize(json)));
            var present=raw.TryGetProperty("organizationHistory",out var history)&&history.ValueKind!=JsonValueKind.Null;
            using var encoded=JsonDocument.Parse(WorkspaceJson.Serialize(document));Assert.Equal(present,encoded.RootElement.TryGetProperty("organizationHistory",out _));Assert.Equal(present?history.GetArrayLength():0,document.OrganizationHistory?.Count??0);
        }
    }
    [Fact]
    public void Native_receipts_cannot_share_task_identity_revision_or_game_event_roles()
    {
        using var fixture=Fixture();var raw=fixture.RootElement.GetProperty("cases").EnumerateArray().Single(row=>row.GetProperty("name").GetString()=="all entity kinds preserve create and partial update snapshots").GetProperty("document");var document=WorkspaceJson.Deserialize(raw.GetRawText());var native=document.OrganizationHistory![0];var task=document.Tasks[0];
        var command=new WorkspaceCommand(native.Command.Id,document.Id,document.Revision,WorkspaceOperation.UpdateTask,task.Id,new(Text:"Changed"));
        var entry=new WorkspaceCommandEntry(command,WorkspaceCommandIdentity.Hash(command),DateTimeOffset.Parse("2026-10-18T13:00:00Z"),ProjectionCommandState.NotRequested,[],TaskSnapshot:task with { Text="Changed",Revision=document.Revision+1,UpdatedAtUtc=DateTimeOffset.Parse("2026-10-18T13:00:00Z") });
        Assert.False(WorkspaceJournalValidation.IsValid(new(document with { Revision=document.Revision+1 },document.Revision+1,[entry],[])));
        command=command with { Id=Guid.NewGuid(),ExpectedRevision=native.Command.ExpectedRevision };entry=entry with { Command=command,PayloadHash=WorkspaceCommandIdentity.Hash(command),TaskSnapshot=entry.TaskSnapshot! with { Revision=command.ExpectedRevision+1 } };Assert.False(WorkspaceJournalValidation.IsValid(new(document,document.Revision,[entry],[])));
        var forged=new WorkspaceJournalEvent(Guid.NewGuid(),native.Command.Id,JournalEventKind.Accepted,DateTimeOffset.Parse("2026-10-18T13:00:00Z"),0,ProjectionFailure.None);Assert.False(WorkspaceJournalValidation.IsValid(new(document,document.Revision,[],[forged])));
    }
}
