using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Application.Notes;
using Habitica.Domain.Notes;
namespace Habitica.Application.Tests.Notes;

public sealed class NotesWireTests
{
    [Fact]
    public void Checked_in_wire_fixture_remains_current_with_the_CSharp_contract()
    {
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}};
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","notes","notes-wire-v1.json")));
        var book=NotesJson.Deserialize(fixture.RootElement.GetProperty("initial").GetRawText());
        foreach(var row in fixture.RootElement.GetProperty("rows").EnumerateArray())
        {
            var command=row.GetProperty("command").Deserialize<NoteCommand>(options)!;Assert.Equal(row.GetProperty("hash").GetString(),NotesJson.Hash(command));
            var result=NotesEngine.Apply(book,command,row.GetProperty("atUtc").GetDateTimeOffset());Assert.True(result.Accepted);book=result.Notebook;
            Assert.Equal(NotesJson.Serialize(NotesJson.Deserialize(row.GetProperty("notebook").GetRawText())),NotesJson.Serialize(book));
        }
    }
    [Fact]
    public void Portable_commands_preserve_exact_json_hashes_and_accepted_notebooks()
    {
        var now=DateTimeOffset.Parse("2026-10-23T00:00:00Z");var book=NotesModel.Empty(NotesModel.Scope("synthetic-profile"));
        var id=Guid.Parse("00000000-0000-4000-8000-000000000021");
        var commands=new[]{
            new NoteCommand(Guid.Parse("00000000-0000-4000-8000-000000000031"),id,0,0,NoteOperation.Create,"Личная 🧥 заметка","# Measurements\r\n```md\n<>&'\"`+\\\n```\n"+string.Concat(Enumerable.Range(0,128).Select(i=>(char)i))),
            new NoteCommand(Guid.Parse("00000000-0000-4000-8000-000000000032"),id,1,1,NoteOperation.Update,"Unicode — 尾",null),
            new NoteCommand(Guid.Parse("00000000-0000-4000-8000-000000000033"),id,2,2,NoteOperation.Append,null,"\r\nnew content\\"),
            new NoteCommand(Guid.Parse("00000000-0000-4000-8000-000000000034"),id,3,3,NoteOperation.Trash),
            new NoteCommand(Guid.Parse("00000000-0000-4000-8000-000000000035"),id,4,4,NoteOperation.Restore),
            new NoteCommand(Guid.Parse("00000000-0000-4000-8000-000000000036"),id,5,5,NoteOperation.Trash),
            new NoteCommand(Guid.Parse("00000000-0000-4000-8000-000000000037"),id,6,6,NoteOperation.Purge)
        };
        var initial=book;var rows=new List<object>();
        foreach(var command in commands){var accepted=NotesEngine.Apply(book,command,now);Assert.True(accepted.Accepted);book=accepted.Notebook;rows.Add(new{command,hash=NotesJson.Hash(command),atUtc=now,notebook=book});}
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true,Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}};
        if(Environment.GetEnvironmentVariable("HABITICA_NOTES_FIXTURES") is {} directory){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"notes-wire-v1.json"),JsonSerializer.Serialize(new{initial,rows},options));}
    }
}
