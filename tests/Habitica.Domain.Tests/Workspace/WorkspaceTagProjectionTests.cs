using System.Text.Json;
using Habitica.Domain.Workspace;
namespace Habitica.Domain.Tests.Workspace;

public sealed class WorkspaceTagProjectionTests
{
    [Fact]
    public void Neutral_ledger_cases_round_trip_or_refuse_identically_without_new_game_tasks()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","tag-projection-v1.json")));
        foreach(var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var json=row.GetProperty("document").GetRawText();
            if(!row.GetProperty("valid").GetBoolean()) { Assert.Throws<JsonException>(()=>WorkspaceJson.Deserialize(json));continue; }
            var document=WorkspaceJson.Deserialize(json);var serialized=WorkspaceJson.Serialize(document);
            Assert.Equal(serialized,WorkspaceJson.Serialize(WorkspaceJson.Deserialize(serialized)));
            Assert.Equal(row.GetProperty("document").GetProperty("tasks").GetArrayLength(),document.Tasks.Count);Assert.Empty(document.Completions);Assert.Empty(document.HabitScores);
            var journal=new WorkspaceJournalSnapshot(document,document.Revision,[],[]);
            Assert.Empty(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(journal)).Commands);
        }
    }

    [Fact]
    public void Absent_ledger_keeps_old_exports_and_native_command_hashes_unchanged()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","tag-projection-v1.json")));
        var row=fixture.RootElement.GetProperty("cases")[0];var document=WorkspaceJson.Deserialize(row.GetProperty("document").GetRawText());
        var noLedger=document with { TagLedger=null };
        Assert.DoesNotContain("tagLedger",WorkspaceJson.Serialize(noLedger));
        using var history=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","organization-history-v1.json")));
        var native=WorkspaceJson.Deserialize(history.RootElement.GetProperty("cases").EnumerateArray().Single(row=>row.GetProperty("name").GetString()=="all entity kinds preserve create and partial update snapshots").GetProperty("document").GetRawText());
        var original=WorkspaceJournalJson.Serialize(new(native,native.Revision,[],[]));var project=native.Projects[0];
        var mapping=document.TagLedger!.Mappings[0] with { Id=Guid.NewGuid(), EntityId=project.Id, Kind=WorkspaceOrganizationKind.Project,
            Revision=native.Revision,DesiredRevision=native.Revision,DesiredName=project.Name,CorrelationAlias=WorkspaceTagProjection.Alias(native.Id,WorkspaceOrganizationKind.Project,project.Id) };
        var tagged=native with { TagLedger=new(document.TagLedger.AccountId,[mapping],[]) };
        var roundTrip=WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(new(tagged,tagged.Revision,[],[])));
        Assert.Equal(original,WorkspaceJournalJson.Serialize(roundTrip with { Document=roundTrip.Document with { TagLedger=null } }));
        Assert.All(roundTrip.Document.OrganizationHistory!,entry=>Assert.Equal(entry.PayloadHash,WorkspaceOrganizationCommands.Hash(entry.Command)));
    }
}
