using Habitica.Domain.Notes;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Notes;

public sealed record NotesProjectAuthority(string OwnerScope,string Source,WorkspaceDocument Document)
{
    public bool Authorizes(NoteProjectLink link)=>NotesModel.ValidProject(link,OwnerScope)&&link.Source==Source&&link.WorkspaceId==Document.Id
        &&WorkspaceRules.Validate(Document).Count==0&&Document.Projects.Any(p=>p.Id==link.ProjectId&&!p.IsArchived&&!p.IsRemoved);
}
// The local account key is a volatile transaction binding, never exported into a Note or command.
public sealed record NotesProjectProof(NotesProjectAuthority Authority,string LocalAccountScope,long StoreVersion);
public sealed record NotesProjectFilter(Guid? ProjectId=null,Guid? WorkspaceId=null,string? Source=null,bool Unassigned=false)
{
    public bool Valid=>Unassigned?ProjectId is null&&WorkspaceId is null&&Source is null:ProjectId is null?WorkspaceId is null&&Source is null:ProjectId!=Guid.Empty&&WorkspaceId is not null&&WorkspaceId!=Guid.Empty&&Source is "browser" or "cloud";
    public bool Matches(NoteProjectLink? project)=>Unassigned?project is null:ProjectId is null||project is not null&&project.ProjectId==ProjectId&&project.WorkspaceId==WorkspaceId&&project.Source==Source;
}
