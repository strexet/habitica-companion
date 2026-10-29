using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Application.Notes;
using Habitica.Domain.Notes;
using Habitica.Domain.Workspace;
using Habitica.Storage;
namespace Habitica.Application.Tests.Notes;
public sealed class NoteProjectTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-29T00:00:00Z");
    private static readonly Guid Workspace=Guid.Parse("00000000-0000-4000-8000-000000000081"),A=Guid.Parse("00000000-0000-4000-8000-000000000082"),B=Guid.Parse("00000000-0000-4000-8000-000000000083"),Note=Guid.Parse("00000000-0000-4000-8000-000000000084");
    private static string Scope=>NotesModel.Scope("synthetic-project-owner");
    private static WorkspaceDocument Document=>new(){Id=Workspace,Projects=[new(A,1,"Duplicate name"),new(B,1,"Duplicate name")]};
    private static NotesProjectAuthority Authority=>new(Scope,"browser",Document);
    private static NoteProjectLink Link(Guid id)=>new(id,Workspace,"browser",Scope);
    private static NoteCommand Command(int sequence,NoteOperation operation,string? title=null,string? markdown=null,NoteProjectLink? project=null,bool? clear=null)=>new(Guid.Parse($"00000000-0000-4000-8000-{sequence:000000000000}"),Note,sequence-1,sequence-1,operation,title,markdown,project,clear);
    [Fact]
    public void Extended_wire_preserves_legacy_hash_shape_and_versioned_metadata_without_rewriting_Markdown()
    {
        var book=NotesModel.Empty(Scope);var initial=book;var rows=new List<object>();
        var commands=new[]{Command(1,NoteOperation.Create,"Project note","# Exact\r\n🧥\n<>&\"\\\n"),Command(2,NoteOperation.Update,project:Link(A)),Command(3,NoteOperation.Update,title:"Changed title"),Command(4,NoteOperation.Update,project:Link(B)),Command(5,NoteOperation.Trash),Command(6,NoteOperation.Restore),Command(7,NoteOperation.Update,clear:true)};
        foreach(var command in commands){var result=NotesEngine.Apply(book,command,Now.AddMinutes(command.ExpectedNotebookRevision),Authority);Assert.True(result.Accepted,result.Code);book=result.Notebook;rows.Add(new{command,hash=NotesJson.Hash(command),atUtc=Now.AddMinutes(command.ExpectedNotebookRevision),notebook=book});}
        Assert.Null(book.Notes[0].Current.Project);Assert.Equal(Link(A),book.Notes[0].History[1].Project);Assert.Equal(Link(A),book.Notes[0].History[2].Project);Assert.Equal(Link(B),book.Notes[0].History[5].Project);Assert.All(book.Notes[0].History,v=>Assert.Equal(commands[0].Markdown,v.Markdown));Assert.Equal(commands[0].Markdown,book.Notes[0].Current.Markdown);Assert.Equal(book.Notes[0].Id,Note);
        Assert.Equal("duplicate",NotesEngine.Apply(book,commands[1],Now).Code);Assert.Equal(NotesJson.Serialize(book),NotesJson.Serialize(NotesJson.Deserialize(NotesJson.Serialize(book))));
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true,Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}};
        var legacy=JsonSerializer.Serialize(commands[0],options);Assert.DoesNotContain("\"project\":",legacy);Assert.DoesNotContain("clearProject",legacy);
        if(Environment.GetEnvironmentVariable("HABITICA_NOTE_PROJECT_FIXTURES") is {} directory){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"notes-project-wire-v2.json"),JsonSerializer.Serialize(new{initial,authority=Authority,rows},options));}
    }
    [Fact]
    public void Checked_in_extended_fixture_has_current_hashes_and_exact_retained_associations()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","notes","notes-project-wire-v2.json")));var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}};var book=NotesJson.Deserialize(fixture.RootElement.GetProperty("initial").GetRawText());
        foreach(var row in fixture.RootElement.GetProperty("rows").EnumerateArray()){var command=row.GetProperty("command").Deserialize<NoteCommand>(options)!;Assert.Equal(row.GetProperty("hash").GetString(),NotesJson.Hash(command));var result=NotesEngine.Apply(book,command,row.GetProperty("atUtc").GetDateTimeOffset(),Authority);Assert.True(result.Accepted);book=result.Notebook;Assert.Equal(NotesJson.Serialize(NotesJson.Deserialize(row.GetProperty("notebook").GetRawText())),NotesJson.Serialize(book));}
    }
    [Fact]
    public void New_links_require_current_exact_owner_source_workspace_and_active_project_but_stale_links_remain_editable_and_clearable()
    {
        var create=Command(1,NoteOperation.Create,"note","exact",Link(A));var empty=NotesModel.Empty(Scope);
        foreach(var link in new[]{Link(A) with {OwnerScope=NotesModel.Scope("other")},Link(A) with {WorkspaceId=Guid.NewGuid()},Link(A) with {Source="cloud"},Link(Guid.NewGuid())})Assert.False(NotesEngine.Apply(empty,create with {Project=link},Now,Authority).Accepted);
        Assert.False(NotesEngine.Apply(empty,create,Now).Accepted);Assert.False(NotesEngine.Apply(empty,create,Now,Authority with {Document=Document with {Projects=[new(A,1,"Duplicate name",IsArchived:true)]}}).Accepted);
        var first=NotesEngine.Apply(empty,create,Now,Authority);Assert.True(first.Accepted);var stale=NotesEngine.Apply(first.Notebook,Command(2,NoteOperation.Update,markdown:"edited"),Now.AddMinutes(1));Assert.True(stale.Accepted);Assert.Equal(Link(A),stale.Notebook.Notes[0].Current.Project);
        var cleared=NotesEngine.Apply(stale.Notebook,Command(3,NoteOperation.Update,clear:true),Now.AddMinutes(2));Assert.True(cleared.Accepted);Assert.Null(cleared.Notebook.Notes[0].Current.Project);Assert.Equal("edited",cleared.Notebook.Notes[0].Current.Markdown);
        Assert.False(NotesEngine.Apply(first.Notebook,Command(2,NoteOperation.Update,project:Link(B),clear:true),Now).Accepted);
        var foreign=first.Notebook with {Notes=[first.Notebook.Notes[0] with {Current=first.Notebook.Notes[0].Current with {Project=Link(A) with {OwnerScope=NotesModel.Scope("other")}}}]};Assert.Throws<JsonException>(()=>NotesJson.Serialize(foreign));
    }
    [Fact]
    public void Project_and_unassigned_filters_run_before_complete_stable_twenty_row_paging()
    {
        var book=NotesModel.Empty(Scope);for(var i=0;i<30;i++){var command=new NoteCommand(Guid.NewGuid(),Guid.NewGuid(),book.Revision,0,NoteOperation.Create,"Same note","Search text",i<25?Link(A):null);var result=NotesEngine.Apply(book,command,Now.AddMinutes(i),Authority);Assert.True(result.Accepted);book=result.Notebook;}
        var filter=new NotesProjectFilter(A,Workspace,"browser");var first=NotesQueries.Search(book,"SEARCH",false,0,null,filter);Assert.Equal(25,first.Total);Assert.Equal(20,first.Results.Count);Assert.Equal(20,first.NextOffset);var next=NotesQueries.Search(book,"search",false,20,book.Revision,filter);Assert.Equal(5,next.Results.Count);Assert.Equal(25,first.Results.Concat(next.Results).Select(n=>n.Id).Distinct().Count());
        Assert.Equal(5,NotesQueries.Search(book,"",false,0,null,new(Unassigned:true)).Total);Assert.Empty(NotesQueries.Search(book,"",false,0,null,new(A,Workspace,"cloud")).Results);Assert.Throws<InvalidOperationException>(()=>NotesQueries.Search(book,"",false,20,null,filter));Assert.Throws<ArgumentException>(()=>NotesQueries.Search(book,"",false,0,null,new(A,Workspace,"browser",true)));
    }
    [Fact]
    public async Task Local_service_requires_bound_atomic_store_and_recovers_a_lost_link_commit_without_revalidating_stale_projects()
    {
        var store=new ProjectStore(Scope);var link=Link(A);var command=Command(1,NoteOperation.Create,"note","body",link);
        Assert.Equal("project_validation_unavailable",(await new NotesService(store).SubmitAsync(command,default)).Code);
        var proof=new NotesProjectProof(Authority,"synthetic-project-owner",7);var service=new NotesService(store,projects:(_,_)=>Task.FromResult<NotesProjectProof?>(proof));
        store.Lose=true;var unknown=await service.SubmitAsync(command,default);Assert.Equal("acceptance_unknown",unknown.Code);Assert.Equal(1,store.LinkedCommits);var recovered=await new NotesService(store).SubmitAsync(command,default);Assert.True(recovered.Accepted);Assert.Equal("duplicate",recovered.Code);Assert.Equal(link,recovered.Notebook.Notes[0].Current.Project);Assert.Equal(1,store.LinkedCommits);
        var restore=new ProjectStore(Scope);Assert.Equal("restored",await new NotesService(restore).ImportAsync(NotesJson.Serialize(recovered.Notebook),default));Assert.Equal(link,(await restore.LoadAsync(default))!.Notes[0].Current.Project);
    }
    private sealed class ProjectStore(string scope):INotesProjectStore
    {
        public string OwnerScope=>scope;private NotesNotebook? _book;public bool Lose;public int LinkedCommits;
        public Task<NotesNotebook?> LoadAsync(CancellationToken token)=>Task.FromResult(_book);
        public Task<bool> TryCommitAsync(long expected,NotesNotebook value,CancellationToken token){if((_book?.Revision??0)!=expected)return Task.FromResult(false);_book=value;return Task.FromResult(true);}
        public async Task<bool> TryCommitProjectAsync(long expected,NotesNotebook value,Guid noteId,string account,long version,CancellationToken token){Assert.Equal("synthetic-project-owner",account);Assert.Equal(7,version);LinkedCommits++;var result=await TryCommitAsync(expected,value,token);if(Lose)throw new IOException("Synthetic lost link acknowledgment.");return result;}
    }

}
