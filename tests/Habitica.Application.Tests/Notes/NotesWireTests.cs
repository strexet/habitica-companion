using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Application.Notes;
using Habitica.Domain.Notes;
namespace Habitica.Application.Tests.Notes;

public sealed class NotesWireTests
{
    [Fact]
    public void Checked_in_folder_fixture_preserves_exact_cross_runtime_commands_and_combined_revisions()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","notes","notes-folder-wire-v3.json")));var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}};
        var book=NotesJson.Deserialize(fixture.RootElement.GetProperty("initial").GetRawText());
        foreach(var row in fixture.RootElement.GetProperty("rows").EnumerateArray())
        {
            if(row.GetProperty("kind").GetString()=="folder"){var command=row.GetProperty("command").Deserialize<NoteFolderCommand>(options)!;Assert.Equal(row.GetProperty("hash").GetString(),NotesJson.Hash(command));var result=NotesFolders.Apply(book,command,row.GetProperty("atUtc").GetDateTimeOffset());Assert.True(result.Accepted,result.Code);book=result.Notebook;}
            else{var command=row.GetProperty("command").Deserialize<NoteCommand>(options)!;Assert.Equal(row.GetProperty("hash").GetString(),NotesJson.Hash(command));var result=NotesEngine.Apply(book,command,row.GetProperty("atUtc").GetDateTimeOffset());Assert.True(result.Accepted,result.Code);book=result.Notebook;}
            Assert.Equal(NotesJson.Serialize(NotesJson.Deserialize(row.GetProperty("notebook").GetRawText())),NotesJson.Serialize(book));
        }
    }
    [Fact]
    public void Folder_commands_have_portable_exact_hashes_and_a_distinct_combined_journal()
    {
        var now=DateTimeOffset.Parse("2026-11-02T00:00:00Z");var scope=NotesModel.Scope("synthetic-folder-wire");var initial=NotesModel.Empty(scope);var book=initial;
        var folder=Guid.Parse("00000000-0000-4000-8000-000000000091");var note=Guid.Parse("00000000-0000-4000-8000-000000000092");var rows=new List<object>();
        var commands=new object[]{
            new NoteFolderCommand(Guid.Parse("00000000-0000-4000-8000-000000000101"),folder,0,0,NoteFolderOperation.Create,scope,"browser","Hotels 🧥"),
            new NoteCommand(Guid.Parse("00000000-0000-4000-8000-000000000102"),note,1,0,NoteOperation.Create,"Hotel review","# Exact\r\n<>&\"\\\n尾",Folder:new(folder,scope,"browser")),
            new NoteFolderCommand(Guid.Parse("00000000-0000-4000-8000-000000000103"),folder,2,1,NoteFolderOperation.Rename,scope,"browser","Interesting places"),
            new NoteCommand(Guid.Parse("00000000-0000-4000-8000-000000000104"),note,3,1,NoteOperation.Update,ClearFolder:true),
            new NoteFolderCommand(Guid.Parse("00000000-0000-4000-8000-000000000105"),folder,4,2,NoteFolderOperation.Remove,scope,"browser")};
        foreach(var command in commands)
        {
            if(command is NoteFolderCommand f){var accepted=NotesFolders.Apply(book,f,now);Assert.True(accepted.Accepted,accepted.Code);book=accepted.Notebook;rows.Add(new{kind="folder",command=f,hash=NotesJson.Hash(f),atUtc=now,notebook=book});}
            else{var n=(NoteCommand)command;var accepted=NotesEngine.Apply(book,n,now);Assert.True(accepted.Accepted,accepted.Code);book=accepted.Notebook;rows.Add(new{kind="note",command=n,hash=NotesJson.Hash(n),atUtc=now,notebook=book});}
        }
        Assert.Equal(5,book.Revision);Assert.Equal(2,book.Receipts.Count);Assert.Equal(3,book.FolderReceipts!.Count);Assert.Equal(book.Notes[0].Current.Markdown,book.Notes[0].History[0].Markdown);
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true,Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}};
        if(Environment.GetEnvironmentVariable("HABITICA_NOTE_FOLDER_FIXTURES") is {} directory){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"notes-folder-wire-v3.json"),JsonSerializer.Serialize(new{initial,rows},options));}
    }
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
