using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Habitica.Domain.Notes;

public enum NoteOperation { Create, Update, Append, Trash, Restore, Purge }
public sealed record NoteProjectLink(Guid ProjectId,Guid WorkspaceId,string Source,string OwnerScope);
public sealed record NoteVersion(long Revision, string Title, string Markdown, bool IsTrashed, DateTimeOffset AtUtc,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] NoteProjectLink? Project=null);
public sealed record PersonalNote(Guid Id, NoteVersion Current, DateTimeOffset CreatedAtUtc, IReadOnlyList<NoteVersion> History);
public sealed record NoteReceipt(Guid CommandId, string PayloadHash, Guid NoteId, long NoteRevision, long NotebookRevision);
public sealed record PurgedNote(Guid Id, long Revision);
public sealed record NotesNotebook(int SchemaVersion, string OwnerScope, long Revision, IReadOnlyList<PersonalNote> Notes,
    IReadOnlyList<NoteReceipt> Receipts, IReadOnlyList<PurgedNote> Purged);
public sealed record NoteCommand(Guid Id, Guid NoteId, long ExpectedNotebookRevision, long ExpectedNoteRevision,
    NoteOperation Operation, string? Title = null, string? Markdown = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] NoteProjectLink? Project=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] bool? ClearProject=null);

public static class NotesModel
{
    public const int BodyBytes = 128 * 1024, HistoryLimit = 16, NotebookBytes = 16 * 1024 * 1024, MaxNotes = 200, MaxReceipts = 2048;
    public const long MaxRevision = 9007199254740991;
    public static string Scope(string userId) => string.IsNullOrWhiteSpace(userId) ? throw new ArgumentException("Account identity required.")
        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();
    public static bool ValidScope(string? scope) => scope is {Length:64} && scope.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool ValidProject(NoteProjectLink? link,string scope)=>link is null||link.ProjectId!=Guid.Empty&&link.WorkspaceId!=Guid.Empty&&link.Source is "browser" or "cloud"&&link.OwnerScope==scope&&ValidScope(link.OwnerScope);
    public static NotesNotebook Empty(string scope) { if(!ValidScope(scope))throw new ArgumentException("Invalid notes scope.");return new(1,scope,0,[],[],[]); }
    public static bool TextValid(string? title,string? body)
    {
        if(string.IsNullOrWhiteSpace(title)||title.Length>256||body is null)return false;
        try{var utf8=new UTF8Encoding(false,true);_ = utf8.GetByteCount(title);return utf8.GetByteCount(body)<=BodyBytes;}catch(EncoderFallbackException){return false;}
    }
    public static void Validate(NotesNotebook book)
    {
        bool At(DateTimeOffset at) => at!=default && at.Offset==TimeSpan.Zero;
        bool Version(NoteVersion? v) => v is not null && v.Revision is >0 and <=MaxRevision && TextValid(v.Title,v.Markdown) && At(v.AtUtc) && ValidProject(v.Project,book.OwnerScope);
        if(book.SchemaVersion!=1||!ValidScope(book.OwnerScope)||book.Revision is <0 or >MaxRevision||book.Notes is null||book.Receipts is null||book.Purged is null
            ||book.Notes.Count>MaxNotes||book.Receipts.Count>MaxReceipts+2*MaxNotes||book.Purged.Count>MaxReceipts)throw new JsonException("Invalid notes notebook.");
        var ids=new HashSet<Guid>();
        foreach(var note in book.Notes)
        {
            if(note is null||note.Id==Guid.Empty||!ids.Add(note.Id)||!Version(note.Current)||!At(note.CreatedAtUtc)||note.CreatedAtUtc>note.Current.AtUtc
                ||note.History is null||note.History.Count!=Math.Min(note.Current.Revision-1,HistoryLimit))throw new JsonException("Invalid note history.");
            var revision=note.Current.Revision-note.History.Count;var previous=note.CreatedAtUtc;
            foreach(var item in note.History)
            {
                if(!Version(item)||item.Revision!=revision++||item.AtUtc<previous||item.AtUtc>note.Current.AtUtc)throw new JsonException("Invalid causal note revision.");
                previous=item.AtUtc;
            }
        }
        foreach(var item in book.Purged)
            if(item is null||item.Id==Guid.Empty||!ids.Add(item.Id)||item.Revision<2||item.Revision>MaxRevision)throw new JsonException("Invalid purged identity.");
        var commands=new HashSet<Guid>();var revisions=new Dictionary<Guid,long>();long notebookRevision=0;
        foreach(var receipt in book.Receipts)
        {
            if(receipt is null||receipt.CommandId==Guid.Empty||!commands.Add(receipt.CommandId)||!ValidScope(receipt.PayloadHash)||!ids.Contains(receipt.NoteId)
                ||receipt.NoteRevision!=revisions.GetValueOrDefault(receipt.NoteId)+1||receipt.NoteRevision>MaxRevision||receipt.NotebookRevision!=++notebookRevision)throw new JsonException("Invalid note receipt.");
            revisions[receipt.NoteId]=receipt.NoteRevision;
        }
        if(book.Notes.Any(note=>note.Current.Revision!=revisions.GetValueOrDefault(note.Id))||book.Purged.Any(note=>note.Revision!=revisions.GetValueOrDefault(note.Id)))throw new JsonException("Note receipt revision mismatch.");
        if(book.Revision!=notebookRevision)throw new JsonException("Missing notebook receipts.");
    }
}
public static class NotesJson
{
    private static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web)
    {UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false)}};
    public static string Serialize(NotesNotebook book)
    {
        NotesModel.Validate(book);var json=JsonSerializer.Serialize(book,Options);
        if(Encoding.UTF8.GetByteCount(json)>NotesModel.NotebookBytes)throw new JsonException("Notes storage quota exceeded.");return json;
    }
    public static NotesNotebook Deserialize(string json)
    {
        if(Encoding.UTF8.GetByteCount(json)>NotesModel.NotebookBytes)throw new JsonException("Notes storage quota exceeded.");
        using var document=JsonDocument.Parse(json);Check(document.RootElement);
        var book=JsonSerializer.Deserialize<NotesNotebook>(json,Options)??throw new JsonException("Invalid notes notebook.");NotesModel.Validate(book);return book;
    }
    private static void Check(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.Ordinal);foreach(var field in value.EnumerateObject()){if(!names.Add(field.Name))throw new JsonException("Duplicate notes field.");Check(field.Value);}}
        else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())Check(item);
    }
    public static string Hash(NoteCommand command) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(command,Options))).ToLowerInvariant();
}
