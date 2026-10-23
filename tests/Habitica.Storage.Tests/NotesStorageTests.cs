using Habitica.Domain.Notes;
using Microsoft.JSInterop;
namespace Habitica.Storage.Tests;

public sealed class NotesStorageTests
{
    private static NotesNotebook Book(string scope)
    {
        var id=Guid.NewGuid();var now=DateTimeOffset.Parse("2026-10-23T00:00:00Z");
        return new(1,scope,1,[new(id,new(1,"note","Unicode 🧥\r\n```md\ntext\n```",false,now),now,[])],[new(Guid.NewGuid(),new string('a',64),id,1,1)],[]);
    }
    [Fact]
    public async Task Adapter_scopes_notes_validates_loaded_owner_and_rejects_stale_CAS()
    {
        var runtime=new Runtime();await using var database=new IndexedDbNotesDatabase(runtime);var scope=NotesModel.Scope("owner");var store=database.ForScope(scope);var book=Book(scope);
        Assert.True(await store.TryCommitAsync(0,book,default));Assert.False(await store.TryCommitAsync(0,book,default));Assert.Equal(NotesJson.Serialize(book),NotesJson.Serialize((await store.LoadAsync(default))!));
        Assert.Null(await database.ForScope(NotesModel.Scope("other")).LoadAsync(default));
        await Assert.ThrowsAsync<ArgumentException>(()=>store.TryCommitAsync(0,Book(NotesModel.Scope("other")),default));
        runtime.ReadOverride=NotesJson.Serialize(Book(NotesModel.Scope("other")));await Assert.ThrowsAsync<InvalidOperationException>(()=>store.LoadAsync(default));runtime.ReadOverride=null;
        await database.ClearAsync(default);Assert.Null(await store.LoadAsync(default));
    }
    private sealed class Runtime:IJSRuntime,IJSObjectReference
    {
        private readonly Dictionary<string,string> _rows=[];public string? ReadOverride{get;set;}
        public ValueTask<T> InvokeAsync<T>(string name,object?[]? args)=>InvokeAsync<T>(name,default,args);
        public ValueTask<T> InvokeAsync<T>(string name,CancellationToken token,object?[]? args)
        {
            token.ThrowIfCancellationRequested();object? result=null;
            if(name=="import")result=this;
            else if(name=="getNotes")result=ReadOverride??_rows.GetValueOrDefault((string)args![0]!);
            else if(name=="clearNotes")_rows.Clear();
            else if(name=="commitNotes")
            {
                var scope=(string)args![0]!;var expected=(long)args[1]!;var old=_rows.GetValueOrDefault(scope);result=(old is null?0:NotesJson.Deserialize(old).Revision)==expected;
                if((bool)result)_rows[scope]=(string)args[2]!;
            }
            return ValueTask.FromResult(result is null?default!:(T)result);
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
