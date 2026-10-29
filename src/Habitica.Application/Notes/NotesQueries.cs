using System.Text;
using Habitica.Domain.Notes;
namespace Habitica.Application.Notes;
public sealed record NotesQueryPage(string OwnerScope,long Revision,IReadOnlyList<NotesSearchItem> Results,int Total,int? NextOffset);
public static class NotesQueries
{
    public static NotesQueryPage Search(NotesNotebook book,string query,bool trash,int offset,long? expectedRevision,NotesProjectFilter? filter=null)
    {
        NotesModel.Validate(book);filter??=new();
        if(query.Length>256||!NotesModel.TextValid("search",query)||offset<0||offset>NotesModel.MaxNotes||!filter.Valid)throw new ArgumentException("Invalid Notes search.");
        if(expectedRevision is not null&&expectedRevision!=book.Revision||offset>0&&expectedRevision!=book.Revision)throw new InvalidOperationException("The Notes page is stale.");
        static string Fold(string value)=>value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();var search=Fold(query);
        var eligible=book.Notes.Where(n=>n.Current.IsTrashed==trash&&filter.Matches(n.Current.Project)&&Fold(n.Current.Title+"\n"+n.Current.Markdown).Contains(search,StringComparison.Ordinal)).ToArray();
        var rows=eligible.Skip(offset).Take(20).Select(n=>new NotesSearchItem(n.Id,n.Current.Title,n.Current.Revision,n.Current.IsTrashed,n.Current.AtUtc,string.Concat(n.Current.Markdown.EnumerateRunes().Take(320).Select(r=>r.ToString())),n.Current.Markdown.EnumerateRunes().Count()>320,n.Current.Project)).ToArray();
        return new(book.OwnerScope,book.Revision,rows,eligible.Length,offset+rows.Length<eligible.Length?offset+rows.Length:null);
    }
}
